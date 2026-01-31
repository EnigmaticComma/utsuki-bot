using App.Attributes;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

using UtsukiBot.Extensions;

namespace App.Services;

[Service]
public class DynamicVoiceChannelService
{
    // Thread safety
    readonly SemaphoreSlim _lock = new(1, 1);
    readonly SemaphoreSlim _nameLock = new(1, 1);

    HashSet<ulong> _dynamicCreatedVoiceChannels = new();
    
    // Name Caching
    List<string> _nameCache = new();
    const string NAME_CACHE_PATH = "DynamicVoiceData/name_cache";
    const string PERSISTENCE_PATH = "DynamicVoiceData/active_channels";

    readonly DiscordSocketClient _discord;
    readonly LoggingService _log;
    readonly GuildSettingsService _guildSettingsService;

    public DynamicVoiceChannelService(DiscordSocketClient discord, LoggingService log, GuildSettingsService guildSettingsService)
    {
        _log = log;
        _discord = discord;
        _guildSettingsService = guildSettingsService;
        _discord.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
        _discord.Ready += OnReady;
    }

    async Task OnReady()
    {
        LoadState();
        await LoadNameCache();
        
        await CleanupInvalidIds();
        await ScanAllGuilds();

        // Populate cache if low
        if (_nameCache.Count < 5)
        {
            _ = Task.Run(ReplenishNameCache);
        }
    }

    async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousVoiceState, SocketVoiceState newVoiceState)
    {
        try 
        {
            var guild = newVoiceState.VoiceChannel?.Guild ?? previousVoiceState.VoiceChannel?.Guild;
            if (guild != null) 
            {
                await EnsureOneEmptyChannel(guild);
                
                // Handle cross-guild movement rare case
                if (previousVoiceState.VoiceChannel != null && newVoiceState.VoiceChannel != null && previousVoiceState.VoiceChannel.Guild.Id != newVoiceState.VoiceChannel.Guild.Id)
                {
                     await EnsureOneEmptyChannel(previousVoiceState.VoiceChannel.Guild);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in OnUserVoiceStateUpdated: {e.Message}");
        }
    }

    async Task EnsureOneEmptyChannel(SocketGuild guild)
    {
        var settings = _guildSettingsService.GetGuildSettings(guild.Id);
        if (!settings.DynamicVoiceSourceId.HasValue) return;

        var hubId = settings.DynamicVoiceSourceId.Value;
        
        await _lock.WaitAsync();
        try
        {
            // 1. Identify all relevant channels in this guild
            var hubChannel = guild.GetVoiceChannel(hubId);
            
            // If Hub is deleted/missing, we stop logic for this guild (safety)
            if (hubChannel == null)
            {
                 Console.WriteLine($"[DynamicVoice] Hub channel '{hubId}' not found in guild '{guild.Name}' ({guild.Id}). Configuration might be stale or cache incomplete.");
                 return;
            }

            var relevantChannels = new List<SocketVoiceChannel>();
            relevantChannels.Add(hubChannel);

            // Find dynamic channels belonging to this guild
            // We use a copy of IDs to effectively filter
            var guildDynamicIds = _dynamicCreatedVoiceChannels.ToList();
            foreach(var id in guildDynamicIds)
            {
                var vc = guild.GetVoiceChannel(id);
                if (vc != null)
                {
                    relevantChannels.Add(vc);
                }
            }

            // 2. Count empty channels
            var emptyChannels = relevantChannels.Where(c => HasAnyUsersOnVoiceChannel(c) == false).ToList();
            int emptyCount = emptyChannels.Count;

            // 3. Logic: Always keep exactly 1 empty channel
            
            // Case A: 0 Empty -> Create new one
            if (emptyCount == 0)
            {
                await CreateDynamicVoiceChannel(hubChannel);
            }
            // Case B: > 1 Empty -> Delete extras (BUT NEVER DELETE HUB)
            else if (emptyCount > 1)
            {
                // Candidates to delete: Empty channels that are NOT the hub
                var candidates = emptyChannels.Where(c => c.Id != hubId).ToList();
                
                // We want to reduce total empty to 1.
                // Current empty = emptyCount. Target = 1.
                // We need to remove (emptyCount - 1).
                int toRemoveCount = emptyCount - 1;
                
                // Take 'toRemoveCount' from candidates
                var toDelete = candidates.Take(toRemoveCount).ToList();
                
                foreach(var channel in toDelete)
                {
                    await DeleteDynamicChannel(channel, save: false);
                }
                
                if (toDelete.Count > 0)
                {
                    SaveState();
                }
            }
            // Case C: Exactly 1 Empty -> Perfect, do nothing.
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in EnsureOneEmptyChannel: {e}");
        }
        finally
        {
            _lock.Release();
        }
    }

    async Task CreateDynamicVoiceChannel(SocketVoiceChannel hubChannel)
    {
        var guild = hubChannel.Guild;
        var name = GetNextChannelName(); // already uses lock internally for name cache
        
        try 
        {
            await guild.CreateVoiceChannelAsync(name, p => {
                p.Bitrate = hubChannel.Bitrate;
                p.CategoryId = hubChannel.CategoryId;
                p.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(hubChannel.PermissionOverwrites);
                p.UserLimit = hubChannel.UserLimit;
                // Position it below the hub/others
                p.Position = hubChannel.Position + 1;
            }).ContinueWith(t => {
                if(t.IsCompletedSuccessfully)
                {
                     var newVc = t.Result;
                    _dynamicCreatedVoiceChannels.Add(newVc.Id);
                    SaveState();
                    Console.WriteLine($"Created dynamic voice '{name}' in {guild.Name}");
                }
                else 
                {
                    Console.WriteLine($"Failed to create channel: {t.Exception}");
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error creating dynamic voice: {e}");
        }
    }

    async Task DeleteDynamicChannel(SocketVoiceChannel channel, bool save = true)
    {
        try {
            await channel.DeleteAsync();
            _dynamicCreatedVoiceChannels.Remove(channel.Id);
            if(save) SaveState();
            Console.WriteLine($"Deleted dynamic channel '{channel.Name}'");
        } catch(Exception e) {
             Console.WriteLine($"Error deleting dynamic channel: {e}");
             // If 404, we should remove it anyway?
             // Simple fallback: if exception, we don't remove? 
             // Ideally we should verify if it exists, but for now adhering to simple logic.
        }
    }

    async Task CleanupInvalidIds()
    {
        await _lock.WaitAsync();
        try 
        {
            var allIds = _dynamicCreatedVoiceChannels.ToList();
            var toRemove = new List<ulong>();
            
            foreach(var id in allIds)
            {
               var ch = await _discord.GetChannelAsync(id);
               if (ch == null)
               {
                   toRemove.Add(id);
               }
            }
            
            if (toRemove.Count > 0)
            {
                foreach(var id in toRemove) _dynamicCreatedVoiceChannels.Remove(id);
                SaveState();
                Console.WriteLine($"Removed {toRemove.Count} invalid/deleted IDs from cache.");
            }
        }
        finally 
        {
            _lock.Release();
        }
    }
    
    async Task ScanAllGuilds()
    {
        // Iterate all guilds and ensure logic consistency
        foreach(var guild in _discord.Guilds)
        {
            await EnsureOneEmptyChannel(guild);
        }
    }

    // State & Cache Helpers
    
    void LoadState()
    {
        // Called in OnReady (Sequential)
        var saved = JsonCache.LoadFromJson<List<ulong>>(PERSISTENCE_PATH);
        if (saved != null)
        {
            _dynamicCreatedVoiceChannels = new HashSet<ulong>(saved);
            Console.WriteLine($"Loaded {_dynamicCreatedVoiceChannels.Count} dynamic channels from persistence.");
        }
    }

    void SaveState()
    {
        // Called inside lock usually
        JsonCache.SaveToJson(PERSISTENCE_PATH, _dynamicCreatedVoiceChannels.ToList());
    }
    
    async Task LoadNameCache()
    {
        await _nameLock.WaitAsync();
        try {
            var saved = JsonCache.LoadFromJson<List<string>>(NAME_CACHE_PATH);
            if (saved != null)
            {
                _nameCache = saved;
                Console.WriteLine($"Loaded {_nameCache.Count} names from cache.");
            }
        } finally { _nameLock.Release(); }
    }

    void SaveNameCache()
    {
         JsonCache.SaveToJson(NAME_CACHE_PATH, _nameCache);
    }

    async Task ReplenishNameCache()
    {
        try 
        {
            Console.WriteLine("Replenishing name cache...");
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5))) 
            {
                string json = await new HttpClient().GetStringAsync("https://random-word-api.vercel.app/api?words=50&type=capitalized", cts.Token);
                string[] result = JsonSerializer.Deserialize<string[]>(json) ?? [];
                
                await _nameLock.WaitAsync();
                try {
                    if(result.Length > 0) 
                    {
                        _nameCache.AddRange(result);
                        SaveNameCache();
                        Console.WriteLine($"Added {result.Length} names to cache. Total: {_nameCache.Count}");
                    }
                } finally { _nameLock.Release(); }
            }
        } 
        catch (Exception e) 
        { 
            Console.WriteLine($"Failed to replenish name cache: {e.Message}");
        }
    }
    
    string GetNextChannelName()
    {
        string suffix = "Generic";
        _nameLock.Wait(); // Blocking wait is acceptable for fast op
        try 
        {
            if (_nameCache.Count > 0)
            {
                suffix = _nameCache[0];
                _nameCache.RemoveAt(0);
                SaveNameCache();
                
                if (_nameCache.Count < 5) _ = Task.Run(ReplenishNameCache);
            }
            else
            {
                suffix = $"Zone {new Random().Next(1000, 9999)}";
                _ = Task.Run(ReplenishNameCache);
            }
        }
        finally { _nameLock.Release(); }
        
        return $"Voice {suffix}";
    }

    static bool HasAnyUsersOnVoiceChannel(SocketVoiceChannel vc)
    {
        return vc.ConnectedUsers.Count > 0;
    }
}
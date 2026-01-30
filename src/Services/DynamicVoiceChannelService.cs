using App.Attributes;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using System.Text.Json;

using UtsukiBot.Extensions;

namespace App.Services;

[Service]
public class DynamicVoiceChannelService
{
    // SocketVoiceChannel _mainVoiceChannel; // Unused - Removed

    HashSet<ulong> _dynamicCreatedVoiceChannels = new();
    
    // Name Caching
    List<string> _nameCache = new();
    const string NAME_CACHE_PATH = "DynamicVoiceData/name_cache";

    readonly DiscordSocketClient _discord;
    readonly LoggingService _log;
    readonly GuildSettingsService _guildSettingsService;

    
    const string PERSISTENCE_PATH = "DynamicVoiceData/active_channels";

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
        LoadNameCache();
        await CleanupOrphans();
        
        // Populate cache if low
        if (_nameCache.Count < 5)
        {
            _ = Task.Run(ReplenishNameCache);
        }
    }

    void LoadState()
    {
        var saved = JsonCache.LoadFromJson<List<ulong>>(PERSISTENCE_PATH);
        if (saved != null)
        {
            _dynamicCreatedVoiceChannels = new HashSet<ulong>(saved);
            Console.WriteLine($"Loaded {_dynamicCreatedVoiceChannels.Count} dynamic channels from persistence.");
        }
    }

    void SaveState()
    {
        JsonCache.SaveToJson(PERSISTENCE_PATH, _dynamicCreatedVoiceChannels.ToList());
    }
    
    void LoadNameCache()
    {
        var saved = JsonCache.LoadFromJson<List<string>>(NAME_CACHE_PATH);
        if (saved != null)
        {
            _nameCache = saved;
            Console.WriteLine($"Loaded {_nameCache.Count} names from cache.");
        }
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
                // Fetch 50 words to keep in reserve
                string json = await new HttpClient().GetStringAsync("https://random-word-api.vercel.app/api?words=50&type=capitalized", cts.Token);
                string[] result = JsonSerializer.Deserialize<string[]>(json) ?? [];
                if(result.Length > 0) 
                {
                    _nameCache.AddRange(result);
                    SaveNameCache();
                    Console.WriteLine($"Added {result.Length} names to cache. Total: {_nameCache.Count}");
                }
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
        if (_nameCache.Count > 0)
        {
            suffix = _nameCache[0];
            _nameCache.RemoveAt(0);
            SaveNameCache();
            
            // Trigger replenish if getting low
            if (_nameCache.Count < 5)
            {
                 _ = Task.Run(ReplenishNameCache);
            }
        }
        else
        {
            // Cache empty, try immediate fetch or fallback
            // We'll trust the Replenish triggered previously, or just use fallback now to avoid blocking
            suffix = $"Zone {new Random().Next(1000, 9999)}";
            // Trigger replenish for next time
            _ = Task.Run(ReplenishNameCache);
        }
        
        return $"Voice {suffix}";
    }


    async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousVoiceState, SocketVoiceState newVoiceState)
    {
        ulong reusedChannelId = 0;
        if (newVoiceState.VoiceChannel != null)
        {
           reusedChannelId = await HandleJoinedVoice(newVoiceState.VoiceChannel, user);
        }
        
        if (previousVoiceState.VoiceChannel != null)
        {
           await HandleLeftVoice(previousVoiceState.VoiceChannel, reusedChannelId);
        }
    }

    /// <returns>The ID of the reused or created channel, or 0 if none.</returns>
    async Task<ulong> HandleJoinedVoice(SocketVoiceChannel joinedChannel, SocketUser user)
    {
        var guildId = joinedChannel.Guild.Id;
        var settings = _guildSettingsService.GetGuildSettings(guildId);
        
        // If this is the "Hub" channel
        if (settings.DynamicVoiceSourceId.HasValue && joinedChannel.Id == settings.DynamicVoiceSourceId.Value)
        {
             IVoiceChannel? targetChannel = null;

             // 1. Try to reuse empty dynamic channel
             foreach(var id in _dynamicCreatedVoiceChannels)
             {
                 if (await _discord.GetChannelAsync(id) is SocketVoiceChannel vc)
                 {
                     if (!HasAnyUsersOnVoiceChannel(vc))
                     {
                         Console.WriteLine($"Reusing existing channel {vc.Name} ({vc.Id})");
                         targetChannel = vc;
                         break;
                     }
                 }
             }

             // 2. If no reuse found, create new
             if (targetChannel == null)
             {
                 targetChannel = await CreateDynamicVoiceChannel(joinedChannel);
             }


             // 3. Move user to target channel
             if (targetChannel != null && user is SocketGuildUser guildUser)
             {
                 await guildUser.ModifyAsync(x => x.ChannelId = targetChannel.Id);
                 return targetChannel.Id;
             }
        }
        return 0;
    }

    async Task HandleLeftVoice(SocketVoiceChannel leftChannel, ulong reusedChannelId)
    {
        // 1. If this was a dynamic channel
        if (_dynamicCreatedVoiceChannels.Contains(leftChannel.Id))
        {
            // If it's empty, delete it UNLESS it was just reused/created for someone else
            if (!HasAnyUsersOnVoiceChannel(leftChannel))
            {
                if (leftChannel.Id == reusedChannelId)
                {
                    Console.WriteLine($"Skipping deletion of {leftChannel.Name} because it was reused/created.");
                    return;
                }
                
                // Allow a small grace period or recheck? 
                // Currently Discord events are usually sequential enough.
                
                await DeleteDynamicChannel(leftChannel);
            }
            return;
        }

        // 2. If this was the Hub channel... (User left Hub)
        // If they were moved (reusedChannelId != 0), they technically "left" the Hub.
        // But HandleJoinedVoice already returns the ID they went to.
        
        var guildId = leftChannel.Guild.Id;
        var settings = _guildSettingsService.GetGuildSettings(guildId);
        
        if (settings.DynamicVoiceSourceId.HasValue && leftChannel.Id == settings.DynamicVoiceSourceId.Value)
        {
             // If we reused a channel, we don't want CleanupOrphans to kill it before they arrive
             await CleanupOrphans(reusedChannelId);
        }
    }

    async Task CleanupOrphans(ulong exemptId = 0)
    {
        // cleanup channels that might be totally empty after a restart
        var toRemove = new List<ulong>();
        foreach (var id in _dynamicCreatedVoiceChannels)
        {
            if (id == exemptId) continue;

            var channel = await _discord.GetChannelAsync(id) as SocketVoiceChannel;
            if (channel == null)
            {
                toRemove.Add(id);
                continue;
            }
            if (!HasAnyUsersOnVoiceChannel(channel))
            {
                await DeleteDynamicChannel(channel, save: false); // Optimize saves
                toRemove.Add(id);
            }
        }
        
        if (toRemove.Count > 0)
        {
            SaveState();
            Console.WriteLine($"Cleaned up {toRemove.Count} orphaned dynamic channels.");
        }
    }

    async Task<IVoiceChannel?> CreateDynamicVoiceChannel(SocketVoiceChannel hubChannel)
    {
        var guild = hubChannel.Guild;
        var name = GetNextChannelName();
        
        try 
        {
            var newVc = await guild.CreateVoiceChannelAsync(name, p => {
                p.Bitrate = hubChannel.Bitrate;
                p.CategoryId = hubChannel.CategoryId;
                p.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(hubChannel.PermissionOverwrites);
                p.UserLimit = hubChannel.UserLimit;
                // Position it below the hub
                p.Position = hubChannel.Position + 1;
            });

            _dynamicCreatedVoiceChannels.Add(newVc.Id);
            SaveState();
            Console.WriteLine($"Created dynamic voice '{name}' in {guild.Name}");
            return newVc;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error creating dynamic voice: {e}");
            return null;
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
        }
    }

    static bool HasAnyUsersOnVoiceChannel(SocketVoiceChannel vc)
    {
        return vc.ConnectedUsers.Count > 0;
    }
}
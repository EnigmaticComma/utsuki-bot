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
        await CleanupOrphans();
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

    /// <returns>The ID of the reused channel, or 0 if created new or none.</returns>
    async Task<ulong> HandleJoinedVoice(SocketVoiceChannel joinedChannel, SocketUser user)
    {
        var guildId = joinedChannel.Guild.Id;
        var settings = _guildSettingsService.GetGuildSettings(guildId);
        
        // If this is the "Hub" channel
        if (settings.DynamicVoiceSourceId.HasValue && joinedChannel.Id == settings.DynamicVoiceSourceId.Value)
        {
             // Check if we can reuse an empty dynamic channel
             foreach(var id in _dynamicCreatedVoiceChannels)
             {
                 if (await _discord.GetChannelAsync(id) is SocketVoiceChannel vc)
                 {
                     if (!HasAnyUsersOnVoiceChannel(vc))
                     {
                         Console.WriteLine($"Reusing existing channel {vc.Name} ({vc.Id})");
                          return vc.Id;
                     }
                 }
             }

             await CreateDynamicVoiceChannel(joinedChannel, user);
        }
        return 0;
    }

    async Task HandleLeftVoice(SocketVoiceChannel leftChannel, ulong reusedChannelId)
    {
        // 1. If this was a dynamic channel
        if (_dynamicCreatedVoiceChannels.Contains(leftChannel.Id))
        {
            // If it's empty, delete it UNLESS it was just reused
            if (!HasAnyUsersOnVoiceChannel(leftChannel))
            {
                if (leftChannel.Id == reusedChannelId)
                {
                    Console.WriteLine($"Skipping deletion of {leftChannel.Name} because it was reused.");
                    return;
                }
                await DeleteDynamicChannel(leftChannel);
            }
            return;
        }

        // 2. If this was the Hub channel... (User left Hub)
        // If they moved to a dynamic channel (reused or new), that dynamic channel is now occupied (or will be).
        // If they left Hub to disconnect, we might have empty channels.
        // We can run cleanup, but we must be careful not to delete the one we just moved them to.
        var guildId = leftChannel.Guild.Id;
        var settings = _guildSettingsService.GetGuildSettings(guildId);
        
        if (settings.DynamicVoiceSourceId.HasValue && leftChannel.Id == settings.DynamicVoiceSourceId.Value)
        {
             // If we reused a channel, we don't want CleanupOrphans to kill it before they arrive
             // But CleanupOrphans checks ConnectedUsers. If the move is pending, it might be 0.
             // We should pass the reused ID to CleanupOrphans to exempt it.
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
                await channel.DeleteAsync();
                toRemove.Add(id);
            }
        }
        
        if (toRemove.Count > 0)
        {
            foreach (var id in toRemove) _dynamicCreatedVoiceChannels.Remove(id);
            SaveState();
            Console.WriteLine($"Cleaned up {toRemove.Count} orphaned dynamic channels.");
        }
    }

    async Task CreateDynamicVoiceChannel(SocketVoiceChannel hubChannel, SocketUser user)
    {
        var guild = hubChannel.Guild;
        
        string suffix = "X";
        try {
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2))) {
                string json = await new HttpClient().GetStringAsync("https://random-word-api.vercel.app/api?words=1&type=capitalized", cts.Token);
                string[] result = JsonSerializer.Deserialize<string[]>(json) ?? [];
                if(result.Length > 0) suffix = result[0];
            }
        } catch { /* ignore */ }

        var name = $"Voice {suffix}";
        
        try 
        {
            var newVc = await guild.CreateVoiceChannelAsync(name, p => {
                p.Bitrate = hubChannel.Bitrate;
                p.CategoryId = hubChannel.CategoryId;
                p.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(hubChannel.PermissionOverwrites);
                p.UserLimit = hubChannel.UserLimit;
                p.Position = hubChannel.Position + 1;
            });

            _dynamicCreatedVoiceChannels.Add(newVc.Id);
            SaveState();
            Console.WriteLine($"Created dynamic voice '{name}' in {guild.Name}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error creating dynamic voice: {e}");
        }
    }

    async Task DeleteDynamicChannel(SocketVoiceChannel channel)
    {
        try {
            await channel.DeleteAsync();
            _dynamicCreatedVoiceChannels.Remove(channel.Id);
            SaveState();
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
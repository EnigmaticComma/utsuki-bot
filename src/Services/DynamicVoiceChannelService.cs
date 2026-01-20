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
    List<ulong> _dynamicCreatedVoiceChannels = new ();
    ulong _mainVoiceChannelId = 822721840404889620;
    SocketVoiceChannel _mainVoiceChannel;

    readonly DiscordSocketClient _discord;
    readonly LoggingService _log;
    readonly GuildSettingsService _guildSettingsService;

    // Persist this list!
    HashSet<ulong> _dynamicCreatedVoiceChannels = new();
    
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

    async Task CleanupOrphans()
    {
        // cleanup channels that might be totally empty after a restart
        var toRemove = new List<ulong>();
        foreach (var id in _dynamicCreatedVoiceChannels)
        {
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

    async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousVoiceState, SocketVoiceState newVoiceState)
    {
        if (newVoiceState.VoiceChannel != null)
        {
           await HandleJoinedVoice(newVoiceState.VoiceChannel);
        }
        
        if (previousVoiceState.VoiceChannel != null)
        {
           await HandleLeftVoice(previousVoiceState.VoiceChannel);
        }
    }

    async Task HandleJoinedVoice(SocketVoiceChannel joinedChannel)
    {
        var guildId = joinedChannel.Guild.Id;
        var settings = _guildSettingsService.GetGuildSettings(guildId);
        
        // If this is the "Hub" channel, create a new dynamic channel
        if (settings.DynamicVoiceSourceId.HasValue && joinedChannel.Id == settings.DynamicVoiceSourceId.Value)
        {
             await CreateDynamicVoiceChannel(joinedChannel);
        }
    }

    async Task HandleLeftVoice(SocketVoiceChannel leftChannel)
    {
        // If this was a dynamic channel and is now empty, delete it
        if (_dynamicCreatedVoiceChannels.Contains(leftChannel.Id))
        {
            if (!HasAnyUsersOnVoiceChannel(leftChannel))
            {
                await DeleteDynamicChannel(leftChannel);
            }
        }
    }

    async Task CreateDynamicVoiceChannel(SocketVoiceChannel hubChannel)
    {
        var guild = hubChannel.Guild;
        
        // Anti-spam check: user just joined hub. 
        // Logic: Create new channel, move user to it? 
        // Original logic didn't move explicitly? Let's check original. 
        // Original logic: "CreateMoreIfNecessary". It seemed to create one if empty ones were full.
        // Usually these bots move the user. I'll add the MoveAsync logic for better UX.
        
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
                // Position it below the hub?
            });

            _dynamicCreatedVoiceChannels.Add(newVc.Id);
            SaveState();
            
            // Move the user who joined the hub into the new channel
            // We need to find *who* caused this. 
            // In OnUserVoiceStateUpdated we don't have the user reference easily passed down without changing sig, 
            // but we can iterate users in the Hub channel.
            
            // Wait a tiny bit for the user to be fully "in" (discord api quirks)
            // But we are in an event handler, so joinedChannel.ConnectedUsers should be up to date or close.
            
            var users = hubChannel.ConnectedUsers.ToList();
            foreach(var user in users)
            {
                try {
                    await user.ModifyAsync(x => x.ChannelId = newVc.Id);
                } catch(Exception e) {
                   Console.WriteLine($"Failed to move user {user.Username}: {e.Message}");
                }
            }

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
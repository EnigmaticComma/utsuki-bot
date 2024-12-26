using StackExchange.Redis;
using UtsukiBot.Extensions;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace UtsukiBot.Services;

public class DynamicVoiceChannelService
{
    readonly DiscordSocketClient _discord;
    List<ulong> _dynamicCreatedVoiceChannels = new ();

    ulong _mainVoiceChannelId = 822721840404889620;
    IVoiceChannel _mainVoiceChannel;
    IDatabase _db;


    public DynamicVoiceChannelService(DiscordSocketClient discord) {
        Console.WriteLine("Creating DynamicVoiceChannelService");
        _discord = discord;
        _discord.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
        _discord.ChannelDestroyed += OnChannelDestroyed;
        try {
            Console.WriteLine($"Trying connecting to redis");
            var redis = ConnectionMultiplexer.Connect("localhost:6379");
            _db = redis.GetDatabase();
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }
    }

    async Task OnChannelDestroyed(SocketChannel c)
    {
        Console.WriteLine($"Channel Destroyed");
        if(c is not SocketVoiceChannel voiceChannel) return;
        UpdateMainVoiceChannel();
        await UpdateVoiceChannelsAsync(voiceChannel);
    }

    async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousVoiceState, SocketVoiceState newVoiceState)
    {
        Console.WriteLine($"OnUserVoiceStateUpdated");
        UpdateMainVoiceChannel();
        await UpdateVoiceChannelsAsync(newVoiceState.VoiceChannel);
    }

    bool UpdateMainVoiceChannel()
    {
        _mainVoiceChannel = _discord.GetChannel(_mainVoiceChannelId) as IVoiceChannel;
        return _mainVoiceChannel != null;
    }

    async Task UpdateVoiceChannelsAsync(SocketVoiceChannel? newVoiceChannel)
    {
        Console.WriteLine($"UpdateVoiceChannelsAsync");
        await CreateMoreIfNecessary(newVoiceChannel);
        await DeleteRemaining();
    }
    async Task CreateMoreIfNecessary(SocketVoiceChannel? newVoiceChannel)
    {
        Console.WriteLine($"CreateMoreIfNecessary");
        if(newVoiceChannel == null) {
            Console.WriteLine($"new voice channel is null");
            return;
        }
        if(_dynamicCreatedVoiceChannels.Count <= 0 && (await _mainVoiceChannel.GetUsersAsync().FlattenAsync()).Any()) {
            await CreateDynamicVoiceChannel(0, newVoiceChannel.Guild.Id);
        }
        for (var i = _dynamicCreatedVoiceChannels.Count - 1; i >= 0; i--) {
            var createdVcId = _dynamicCreatedVoiceChannels[i];
            if(await _discord.GetChannelAsync(createdVcId) is not IVoiceChannel voiceChannel) continue;
            if(!(await HasAnyUsers(voiceChannel))) break;
            if(await CreateDynamicVoiceChannel(i, voiceChannel.GuildId)) return;
        }
    }

    /// <returns>true if created</returns>
    async Task<bool> CreateDynamicVoiceChannel(int i, ulong guildId)
    {
        var targetGuild = _discord.GetGuild(guildId);
        Console.WriteLine($"creating dynamic voice channel {i} at guild {targetGuild.Name}");
        var name = $"Voice {i + 2}";
        RestVoiceChannel? newVc = default;
        try {
            newVc = await targetGuild.CreateVoiceChannelAsync(name, p=>CopyChannelProperties(_mainVoiceChannel, p));
            if(newVc == null) {
                Console.WriteLine($"Failed to create new voice channel '{name}'");
                return false;
            }
        }
        catch (Exception e) {
            Console.WriteLine(e);
            return false;
        }
        _dynamicCreatedVoiceChannels.Add(newVc.Id);
        Console.WriteLine($"Created new voice channel '{name}'");
        return true;
    }

    void CopyChannelProperties(IVoiceChannel originalVc, VoiceChannelProperties p)
    {
        p.Bitrate = originalVc.Bitrate;
        p.Position = originalVc.Position + 1;
        p.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(originalVc.PermissionOverwrites);
        p.CategoryId = originalVc.CategoryId;
        p.UserLimit = originalVc.UserLimit;
    }

    async Task DeleteRemaining()
    {
        for (var i = _dynamicCreatedVoiceChannels.Count - 1; i >= 0; i--) {
            var createdVcId = _dynamicCreatedVoiceChannels[i];

            if(await _discord.GetChannelAsync(createdVcId) is not IVoiceChannel voiceChannel) continue;
            if(await HasAnyUsers(voiceChannel)) continue;

            if(_dynamicCreatedVoiceChannels.GetIfInRange(i - 1, out var id)) {
                if(await _discord.GetChannelAsync(id) is not IVoiceChannel otherVoiceChannel) continue;
                if(await HasAnyUsers(otherVoiceChannel)) {
                    break;
                }
            }

            Console.WriteLine($"Deleting channel '{voiceChannel.Name}'");
            await voiceChannel.DeleteAsync();
            _dynamicCreatedVoiceChannels.Remove(createdVcId);
        }
    }
    static async Task<bool> HasAnyUsers(IVoiceChannel vc)
    {
        var users = await vc.GetUsersAsync().FlattenAsync();
        return users.Any();
    }
}
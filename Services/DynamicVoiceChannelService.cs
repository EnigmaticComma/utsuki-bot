using UtsukiBot.Extensions;

namespace UtsukiBot.Services;

using Discord;
using StackExchange.Redis;
using Discord.WebSocket;

public class DynamicVoiceChannelService
{
    readonly DiscordSocketClient _discord;
    List<ulong> _dynamicCreatedVoiceChannels = new ();

    ulong _mainVoiceChannelId = 822721840404889620;
    IVoiceChannel _mainVoiceChannel;
    IDatabase db;

    public DynamicVoiceChannelService(DiscordSocketClient discord) {
        _discord = discord;
        _discord.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
        _discord.ChannelDestroyed += OnChannelDestroyed;
        var redis = ConnectionMultiplexer.Connect("redis");
        db = redis.GetDatabase();
        UpdateVoiceChannelsAsync().ConfigureAwait(true);
    }

    async Task OnChannelDestroyed(SocketChannel c)
    {
        if(await _discord.GetChannelAsync(_mainVoiceChannelId) is not IVoiceChannel _mainVoiceChannel) return;
        await UpdateVoiceChannelsAsync();
    }

    async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousVoiceState,SocketVoiceState newVoiceState)
    {
        if(await _discord.GetChannelAsync(_mainVoiceChannelId) is not IVoiceChannel _mainVoiceChannel) return;
        await UpdateVoiceChannelsAsync();
    }

    async Task UpdateVoiceChannelsAsync()
    {
        await CreateMoreIfNecessary();
        await DeleteRemaining();
    }
    async Task CreateMoreIfNecessary()
    {
        for (var i = _dynamicCreatedVoiceChannels.Count - 1; i >= 0; i--) {
            var createdVcId = _dynamicCreatedVoiceChannels[i];
            if(await _discord.GetChannelAsync(createdVcId) is not IVoiceChannel voiceChannel) continue;
            if(!(await HasAnyUsers(voiceChannel))) break;
            var name = $"Voice {i + 2}";
            var newVc = await _discord.GetGuild(voiceChannel.GuildId).CreateVoiceChannelAsync(name, p=>CopyChannelProperties(_mainVoiceChannel, p));
            if(newVc == null) {
                Console.WriteLine($"Failed to create new voice channel '{name}'");
                return;
            }
            _dynamicCreatedVoiceChannels.Add(newVc.Id);
            Console.WriteLine($"Created new voice channel '{name}'");
        }
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
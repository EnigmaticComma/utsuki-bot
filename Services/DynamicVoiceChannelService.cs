using System.Net;
using System.Text.Json;
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
        try {
            Console.WriteLine($"Trying connecting to redis");
            var redis = ConnectionMultiplexer.Connect("localhost:6379");
            _db = redis.GetDatabase();
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }
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
        if(_dynamicCreatedVoiceChannels.Count <= 0 && await HasAnyUsersOnVoiceChannel(_mainVoiceChannel)) {
            await CreateDynamicVoiceChannel(0, newVoiceChannel.Guild.Id);
            return;
        }
        for (var i = _dynamicCreatedVoiceChannels.Count - 1; i >= 0; i--) {
            var createdVcId = _dynamicCreatedVoiceChannels[i];
            if(await _discord.GetChannelAsync(createdVcId) is not IVoiceChannel voiceChannel) continue;
            if(!(await HasAnyUsersOnVoiceChannel(voiceChannel))) break;
            if(await CreateDynamicVoiceChannel(i, voiceChannel.GuildId)) return;
        }
    }

    /// <returns>true if created</returns>
    async Task<bool> CreateDynamicVoiceChannel(int i, ulong guildId)
    {
        var targetGuild = _discord.GetGuild(guildId);

        // get to URL "https://random-word-api.vercel.app/api?words=1&type=capitalized":
        string sufix = "X";
        var json = await new HttpClient().GetStringAsync("https://random-word-api.vercel.app/api?words=1&type=capitalized");
        string[] resultado = JsonSerializer.Deserialize<string[]>(json) ?? [];
        if(resultado != null && resultado.Length > 0) {
            sufix = resultado[0];
        }

        Console.WriteLine($"creating dynamic voice channel {i} at guild {targetGuild.Name} with name '{sufix}'");
        var name = $"Voice {sufix}";
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
            if(i == 0 && await HasAnyUsersOnVoiceChannel(_mainVoiceChannel)) {
                Console.WriteLine("Will not delete the first dynamic created channel");
                return;
            }

            var createdVcId = _dynamicCreatedVoiceChannels[i];

            if(await _discord.GetChannelAsync(createdVcId) is not IVoiceChannel voiceChannel) continue;
            if(await HasAnyUsersOnVoiceChannel(voiceChannel)) continue;

            if(_dynamicCreatedVoiceChannels.GetIfInRange(i - 1, out var id)) {
                if(await _discord.GetChannelAsync(id) is not IVoiceChannel otherVoiceChannel) continue;
                if(await HasAnyUsersOnVoiceChannel(otherVoiceChannel)) {
                    continue;
                }
            }

            Console.WriteLine($"Deleting channel '{voiceChannel.Name}'");
            await voiceChannel.DeleteAsync();
            _dynamicCreatedVoiceChannels.Remove(createdVcId);
        }
    }
    static async Task<bool> HasAnyUsersOnVoiceChannel(IVoiceChannel vc)
    {
        var users = await vc.GetUsersAsync().FlattenAsync();
        return users.Any();
    }
}
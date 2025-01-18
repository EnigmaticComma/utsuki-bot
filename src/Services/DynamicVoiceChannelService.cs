using System.Text.Json;
using UtsukiBot.Extensions;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace App.Services;

public class DynamicVoiceChannelService
{
    List<ulong> _dynamicCreatedVoiceChannels = new ();
    ulong _mainVoiceChannelId = 822721840404889620;
    SocketVoiceChannel _mainVoiceChannel;

    readonly DbService _db;
    readonly DiscordSocketClient _discord;

    public DynamicVoiceChannelService(DiscordSocketClient discord, DbService db)
    {
        _db = db;
        Console.WriteLine("Creating DynamicVoiceChannelService");
        _discord = discord;
        _discord.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
    }

    async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousVoiceState, SocketVoiceState newVoiceState)
    {
        Console.WriteLine($"OnUserVoiceStateUpdated");
        UpdateMainVoiceChannel();
        await UpdateVoiceChannelsAsync(newVoiceState.VoiceChannel);
    }

    bool UpdateMainVoiceChannel()
    {
        _mainVoiceChannel = _discord.GetChannel(_mainVoiceChannelId) as SocketVoiceChannel;
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
        if(_dynamicCreatedVoiceChannels.Count <= 0 && HasAnyUsersOnVoiceChannel(_mainVoiceChannel)) {
            await CreateDynamicVoiceChannel(0, newVoiceChannel.Guild.Id);
            return;
        }
        for (var i = _dynamicCreatedVoiceChannels.Count - 1; i >= 0; i--) {
            var createdVcId = _dynamicCreatedVoiceChannels[i];
            if(await _discord.GetChannelAsync(createdVcId) is not SocketVoiceChannel voiceChannel) continue;
            if(!(HasAnyUsersOnVoiceChannel(voiceChannel))) break;
            if(await CreateDynamicVoiceChannel(i, voiceChannel.Guild.Id)) return;
        }
    }

    /// <returns>true if created</returns>
    async Task<bool> CreateDynamicVoiceChannel(int i, ulong guildId)
    {
        var targetGuild = _discord.GetGuild(guildId);

        // get to URL "https://random-word-api.vercel.app/api?words=1&type=capitalized":
        string sufix = "X";
        string json = default;
        using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            json = await new HttpClient().GetStringAsync("https://random-word-api.vercel.app/api?words=1&type=capitalized", cts.Token);
        }

        string[] resultado = JsonSerializer.Deserialize<string[]>(json) ?? [];
        if(resultado.Length > 0) {
            sufix = resultado[0];
        }

        Console.WriteLine($"creating dynamic voice channel {i} at guild {targetGuild.Name} with name '{sufix}'");
        var name = $"Voice {sufix}";
        RestVoiceChannel? newVc = default;
        try {
            newVc = await targetGuild.CreateVoiceChannelAsync(name, p=> CopyChannelProperties(_mainVoiceChannel, p));
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
            Console.WriteLine($"check for channel index {i}");
            if(i == 0 && HasAnyUsersOnVoiceChannel(_mainVoiceChannel)) {
                Console.WriteLine("Will not delete the first dynamic created channel");
                return;
            }

            var createdVcId = _dynamicCreatedVoiceChannels[i];

            if(await _discord.GetChannelAsync(createdVcId) is not SocketVoiceChannel voiceChannel) {
                Console.WriteLine("Channel not found");
                continue;
            }
            if(HasAnyUsersOnVoiceChannel(voiceChannel)) {
                Console.WriteLine("Channel has users");
                continue;
            }

            if(_dynamicCreatedVoiceChannels.GetIfInRange(i - 1, out var id)) {
                if(await _discord.GetChannelAsync(id) is not SocketVoiceChannel otherVoiceChannel) {
                    Console.WriteLine("Other channel not found");
                    continue;
                }
                if(HasAnyUsersOnVoiceChannel(otherVoiceChannel)) {
                    Console.WriteLine("Other channel has users");
                    continue;
                }
            }

            Console.WriteLine($"Deleting channel '{voiceChannel.Name}'");
            await voiceChannel.DeleteAsync();
            _dynamicCreatedVoiceChannels.Remove(createdVcId);
        }
    }
    static bool HasAnyUsersOnVoiceChannel(SocketVoiceChannel vc)
    {
        return vc.ConnectedUsers.Count > 0;
    }
}
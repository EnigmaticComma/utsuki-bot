using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Timer = System.Timers.Timer;

namespace App.Services;

public class DiscordClientService
{
    const int MessagesToRemember = 100000;
    static Timer _timer;
    static List<ulong> _rememberChannelIds = new();
    static readonly Random _random = new ();
    readonly DiscordSocketClient _client;

    public DiscordClientService()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig {
            LogLevel = LogSeverity.Info,
            GatewayIntents = GatewayIntents.AllUnprivileged
                             | GatewayIntents.MessageContent | GatewayIntents.GuildPresences | GatewayIntents.GuildMembers,
        });
        _client.Log += Log;
        _client.Ready += OnReady;
        _client.SlashCommandExecuted += SlashCommandHandler;

        _timer = new Timer(7 * 60 * 60 * 1000); // 6 hours in milliseconds
        _timer.Elapsed += async (sender, e) => await ScheduledTask();
        _timer.Start();
    }


    #region Commands

    static Task Log(LogMessage msg) {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    async Task OnReady() {
        foreach (var guild in _client.Guilds) {
            await guild.CreateApplicationCommandAsync(new SlashCommandBuilder()
                .WithName("remember")
                .WithDescription("Activate or deactivate the remember routine in this channel")
                .AddOption("action", ApplicationCommandOptionType.String, "Use 'on' to activate or 'off' to deactivate", true)
                .Build());
        }

        Console.WriteLine("Bot is ready!");
    }

    [CommandContextType(InteractionContextType.Guild)]
    [Discord.Commands.RequireUserPermission(GuildPermission.Administrator)]
    async Task SlashCommandHandler(SocketSlashCommand command) {
        if (command.Data.Name == "remember") {
            var action = (string)command.Data.Options.First(o => o.Name == "action").Value;
            var channelId = command.Channel.Id;

            if (action.ToLower() == "on") {
                if (!_rememberChannelIds.Contains(channelId)) {
                    _rememberChannelIds.Add(channelId);
                    await command.RespondAsync($"Remember routine activated for channel {command.Channel.Name}.");
                }
                else {
                    await command.RespondAsync($"Remember routine is already active for channel {command.Channel.Name}.");
                }
                await ScheduledTask();
            }
            else if (action.ToLower() == "off") {
                if (_rememberChannelIds.Contains(channelId)) {
                    _rememberChannelIds.Remove(channelId);
                    await command.RespondAsync($"Remember routine deactivated for channel {command.Channel.Name}.");
                }
                else {
                    await command.RespondAsync($"Remember routine is not active for channel {command.Channel.Name}.");
                }
            }
        }
    }

    async Task ScheduledTask() {
        Console.WriteLine("Starting to remember messages");
        foreach (var channelId in _rememberChannelIds) {
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null) continue;

            Console.WriteLine("Looking for messages in channel with ID " + channelId);

            var messages = (await channel.GetMessagesAsync(MessagesToRemember, CacheMode.AllowDownload).FlattenAsync()).ToArray();
            Console.WriteLine($"Downloaded {messages.Length} messages");
            var userMessages = messages.Where(m => !m.Author.IsBot && (!string.IsNullOrEmpty(m.Content) || m.Attachments.Count > 0)).ToList();

            if (userMessages.Count < 5 || userMessages.TakeLast(30).Any(m => m.Author.IsBot)) {
                Console.WriteLine($"No enough messages to remember in this channel {channelId}, or there are bot messages in between");
                continue;
            }

            var randomMessage = userMessages[_random.Next(userMessages.Count)];

            var embed = new EmbedBuilder()
                .WithAuthor(randomMessage.Author.GlobalName ?? randomMessage.Author.Username, randomMessage.Author.GetAvatarUrl() ?? randomMessage.Author.GetDefaultAvatarUrl())
                .WithDescription(randomMessage.Content)
                .WithTimestamp(randomMessage.Timestamp);

            if (randomMessage.Attachments.Count > 0) {
                embed.WithThumbnailUrl(randomMessage.Attachments.First().Url);
            }

            Console.WriteLine($"Will send message on channel id {channelId}, content: {randomMessage.Content}");
            await channel.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None, messageReference: new MessageReference(randomMessage.Id));
        }
    }

    #endregion Commands

}
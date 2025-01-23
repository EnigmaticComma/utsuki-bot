using System.Reflection;
using App.HungerGames;
using App.Twitter;
using Discord;
using Discord.Interactions;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using App.Services;
using Microsoft.Extensions.Configuration;
using Timer = System.Timers.Timer;

namespace App;

static class Program {

    public static readonly Version VERSION = Assembly.GetExecutingAssembly().GetName().Version ?? new ("7.0.0");

    const int MessagesToRemember = 100000;

    static Timer _timer;
    static DiscordSocketClient _client;
    static List<ulong> _rememberChannelIds = new();
    static ServiceProvider _serviceProvider;
    static readonly Random _random = new ();
    static IConfigurationRoot Configuration;

    public static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();
        Configuration = builder.Build();

        // TODO migrate to a service
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig {
                LogLevel = LogSeverity.Info,
                GatewayIntents = GatewayIntents.All,
            });
            _client.Log += Log;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += SlashCommandHandler;

            _timer = new Timer(7 * 60 * 60 * 1000); // 6 hours in milliseconds
            _timer.Elapsed += async (sender, e) => await ScheduledTask();
            _timer.Start();
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _serviceProvider.GetRequiredService<DbService>();
        _serviceProvider.GetRequiredService<GuildSettingsService>();
        _serviceProvider.GetRequiredService<LoggingService>();
        _serviceProvider.GetRequiredService<CommandHandler>();
        _serviceProvider.GetRequiredService<ChatService>();
        _serviceProvider.GetRequiredService<HungerGameService>();
        _serviceProvider.GetRequiredService<VoiceService>();
        _serviceProvider.GetRequiredService<ExchangeService>();
        _serviceProvider.GetRequiredService<BackupService>();
        _serviceProvider.GetRequiredService<ModeratorService>();
        _serviceProvider.GetRequiredService<AutoReactService>();
        _serviceProvider.GetRequiredService<SleepService>();

        _serviceProvider.GetRequiredService<DynamicVoiceChannelService>();

        await _serviceProvider.GetRequiredService<StartupService>().StartAsync();
        Console.WriteLine("Started");
        await Task.Delay(-1);                               // Keep the program alive
    }


    static void ConfigureServices(IServiceCollection services) {
        services
        .AddSingleton<DbService>()
        .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {                                       // Add discord to the collection
            LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
            MessageCacheSize = 1000             // Cache 1,000 messages per channel
        }))
        .AddSingleton(new CommandService(new CommandServiceConfig
        {                                       // Add the command service to the collection
            LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
            DefaultRunMode = Discord.Commands.RunMode.Async,     // Force all commands to run async by default
        }))
        .AddSingleton<DynamicVoiceChannelService>()
        .AddSingleton<Random>()
        .AddSingleton<StartupService>()
        .AddSingleton<GuildSettingsService>()
        .AddSingleton<LoggingService>()
        .AddSingleton<CommandHandler>()
        .AddSingleton<StartupService>()
        .AddSingleton<Random>()
        .AddSingleton<VoiceService>()
        .AddSingleton<ExchangeService>()
        .AddSingleton<ChatService>()
        .AddSingleton<HungerGameService>()
        .AddSingleton<WeatherService>()
        .AddSingleton<BackupService>()
        .AddSingleton<ModeratorService>()
        .AddSingleton<AutoReactService>()
        .AddSingleton<SleepService>()
        .AddSingleton(Configuration)
        ;
    }

    #region Commands

    static Task Log(LogMessage msg) {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    static async Task OnReady() {
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
    static async Task SlashCommandHandler(SocketSlashCommand command) {
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

    static async Task ScheduledTask() {
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

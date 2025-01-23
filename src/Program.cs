using System.Reflection;
using App.HungerGames;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using App.Services;
using Microsoft.Extensions.Configuration;

namespace App;

static class Program {

    public static readonly Version VERSION = Assembly.GetExecutingAssembly().GetName().Version ?? new ("7.0.0");

    static ServiceProvider _serviceProvider;
    static IConfigurationRoot Configuration;

    public static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();
        Configuration = builder.Build();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.GetRequiredService<StartupService>().StartAsync();
        Console.WriteLine("Started");
        await Task.Delay(-1);                               // Keep the program alive
    }


    static void ConfigureServices(IServiceCollection services) {
        services
        .AddTransient<DbService>()
        .AddActivatedSingleton<StartupService>()
        .AddActivatedSingleton<DiscordClientService>()
        .AddSingleton(new CommandService(new CommandServiceConfig
        {                                       // Add the command service to the collection
            LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
            DefaultRunMode = Discord.Commands.RunMode.Async,     // Force all commands to run async by default
        }))
        .AddActivatedSingleton<DynamicVoiceChannelService>()
        .AddActivatedSingleton<GuildSettingsService>()
        .AddActivatedSingleton<LoggingService>()
        .AddActivatedSingleton<CommandHandler>()
        .AddSingleton<Random>()
        .AddActivatedSingleton<VoiceService>()
        .AddActivatedSingleton<ExchangeService>()
        .AddActivatedSingleton<ChatService>()
        .AddActivatedSingleton<HungerGameService>()
        .AddActivatedSingleton<WeatherService>()
        .AddActivatedSingleton<BackupService>()
        .AddActivatedSingleton<ModeratorService>()
        .AddActivatedSingleton<AutoReactService>()
        .AddActivatedSingleton<SleepService>()
        .AddSingleton(Configuration)
        ;
    }

}

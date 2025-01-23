﻿using System.Reflection;
using App.HungerGames;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using App.Services;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace App;

static class Program {

    public static readonly Version VERSION = Assembly.GetExecutingAssembly().GetName().Version ?? new ("7.0.0");

    static IConfigurationRoot Configuration;

    public static async Task Main(string[] args)
    {
        Configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var builder = Host.CreateApplicationBuilder(args);

        ConfigureServices(builder.Services);

        var host = builder.Build();

        host.Services.GetRequiredService<DiscordSocketClient>();
        host.Services.GetRequiredService<CommandService>();

        await host.Services.GetRequiredService<StartupService>().StartAsync();

        Console.WriteLine($"Program Started, v{VERSION}");
        await host.RunAsync();
    }


    static void ConfigureServices(IServiceCollection services) {
        services
        .AddTransient<DbService>()
        .AddSingleton(Configuration)
        .AddSingleton<Random>()
        .AddSingleton<StartupService>()
        .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig {
            LogLevel = LogSeverity.Info,
            GatewayIntents = GatewayIntents.AllUnprivileged
                             | GatewayIntents.MessageContent | GatewayIntents.GuildPresences | GatewayIntents.GuildMembers,
        }))
        .AddSingleton(new CommandService(new CommandServiceConfig
        {                                       // Add the command service to the collection
            LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
            DefaultRunMode = RunMode.Async,     // Force all commands to run async by default
        }))
        .AddActivatedSingleton<AIAnswerService>()
        .AddActivatedSingleton<WipServices>()
        .AddActivatedSingleton<DynamicVoiceChannelService>()
        .AddActivatedSingleton<GuildSettingsService>()
        .AddActivatedSingleton<LoggingService>()
        .AddActivatedSingleton<CommandHandler>()
        .AddActivatedSingleton<VoiceService>()
        .AddActivatedSingleton<ExchangeService>()
        .AddActivatedSingleton<ChatService>()
        .AddActivatedSingleton<HungerGameService>()
        .AddActivatedSingleton<WeatherService>()
        .AddActivatedSingleton<BackupService>()
        .AddActivatedSingleton<ModeratorService>()
        .AddActivatedSingleton<AutoReactService>()
        .AddActivatedSingleton<SleepService>()
        ;
    }

}

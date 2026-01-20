using System.Reflection;
using App.Extensions;
using App.HungerGames;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using App.Services;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RunMode = Discord.Commands.RunMode;

namespace App;

internal static class Program {

    public static readonly Version VERSION = Assembly.GetExecutingAssembly().GetName().Version ?? new ("7.0.0");

    static IConfigurationRoot Configuration;

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"Program Started, v{VERSION}");
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var discordToken = Configuration["DISCORD_TOKEN_UTSUKI"];
        if (string.IsNullOrWhiteSpace(discordToken))
            throw new Exception("Bot cannot be started without the bot's token into enviroment variable.");

        var aiToken = Configuration["AI_TOKEN"];
        if (string.IsNullOrWhiteSpace(aiToken))
            throw new Exception("Bot cannot be started without the AI token into enviroment variable.");

        var builder = Host.CreateApplicationBuilder(args);
        ConfigureServices(builder.Services);
        var host = builder.Build();

        var client = host.Services.GetRequiredService<DiscordSocketClient>();
        await client.LoginAsync(TokenType.Bot, discordToken);
        await client.StartAsync();

        var commands = host.Services.GetRequiredService<CommandService>();
        await commands.AddModulesAsync(Assembly.GetEntryAssembly(), host.Services);

        await host.Services.GetRequiredService<InteractionHandler>().InitializeAsync();

        await host.RunAsync();
    }


    static void ConfigureServices(IServiceCollection services) {
        services
        .AddSingleton(Configuration)
        .AddSingleton<Random>()
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
        .AddActivatedSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
        .AddAnnotatedServices(Assembly.GetExecutingAssembly())
        ;
    }

}

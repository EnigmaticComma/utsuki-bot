namespace App;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

public class StartupService {
    readonly IServiceProvider _provider;
    readonly DiscordSocketClient _discord;
    readonly CommandService _commands;
    readonly IConfigurationRoot _config;

    // DiscordSocketClient, CommandService, and IConfigurationRoot are injected automatically from the IServiceProvider
    public StartupService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands, IConfigurationRoot config) {
        _provider = provider;
        _discord = discord;
        _commands = commands;
        _config = config;
    }

    public async Task StartAsync() {
        var discordToken = _config["DISCORD_TOKEN_UTSUKI"];
        if (string.IsNullOrWhiteSpace(discordToken))
            throw new Exception("Please enter the bot's token into enviroment variable.");

        await _discord.LoginAsync(TokenType.Bot, discordToken);     // Login to discord
        await _discord.StartAsync();                                // Connect to the websocket

        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);     // Load commands and modules into the command service
    }
}

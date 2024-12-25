using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace UtsukiBot.Services;

public class StartupService {
    readonly IServiceProvider _provider;
    readonly DiscordSocketClient _discord;
    readonly CommandService _commands;

    public StartupService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands) {
        this._provider = provider;
        this._discord = discord;
        this._commands = commands;
    }

    public async Task StartAsync()
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN_UTSUKI");
        if (string.IsNullOrEmpty(token)) {
            Console.WriteLine("No token provided for Discord.");
            return;
        }
        await this._discord.LoginAsync(TokenType.Bot, token);
        await this._discord.StartAsync();
        await this._commands.AddModulesAsync(Assembly.GetEntryAssembly(), this._provider);
    }
}
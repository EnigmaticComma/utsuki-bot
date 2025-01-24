using System.Reflection;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Discord;

namespace App {
    public class CommandHandler {
        readonly DiscordSocketClient _discord;
        readonly CommandService _commands;
        readonly IConfigurationRoot _config;
        readonly IServiceProvider _provider;
        readonly LoggingService _log;

        // DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public CommandHandler(DiscordSocketClient discord, LoggingService log, CommandService commands, IConfigurationRoot config, IServiceProvider provider) {
            _discord = discord;
            _log = log;
            _commands = commands;
            _config = config;
            _provider = provider;

            _log.Info($"Bot prefix is '{_config["prefix"]}'");

            _discord.MessageReceived += OnMessageReceivedAsync;
        }

        async Task OnMessageReceivedAsync(SocketMessage s)
        {
            if (s.Author.IsBot) return;
            if (s is not SocketUserMessage msg) return;
            var context = new SocketCommandContext(_discord, msg);     // Create the command context

            int argPos = 0;     // Check if the message has a valid command prefix
            if (msg.HasMentionPrefix(_discord.CurrentUser, ref argPos)
                || (!string.IsNullOrEmpty(_config["prefix"]) && msg.HasStringPrefix(_config["prefix"], ref argPos)))
            {
                var result = await _commands.ExecuteAsync(context, argPos, _provider);     // Execute the command

                if (!result.IsSuccess) { // If not successful, reply with the error.
                    //await context.Channel.SendMessageAsync(result.ToString());
                    await msg.AddReactionAsync(new Emoji("❔"));
                }     
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using App.Services;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace App.Twitter {
	public class TwitterService {
		readonly DiscordSocketClient _discord;
		readonly IConfigurationRoot _config;
		readonly LoggingService _log;
		readonly TwitterApi _twitterApi;

		public TwitterService(DiscordSocketClient discord, IConfigurationRoot config, LoggingService loggingService) {
			_config = config;
			_discord = discord;
			_log = loggingService;
			
			_twitterApi = new TwitterApi(
				_config["twitter:api-key"],
				_config["twitter:api-key-secret"],
				_config["twitter:access-token"],
				_config["twitter:access-token-secret"]
			);
			
			_discord.MessageReceived += DiscordOnMessageReceived;
		}

		async Task DiscordOnMessageReceived(SocketMessage msg) {
			if (msg.Source != MessageSource.User) return;
			ulong twitterAutoPostChannelId = ulong.Parse(_config["twitter:auto-post-channel-id"]);

			if (msg.Channel.Id != twitterAutoPostChannelId) return;

			var response = await _twitterApi.Tweet(msg.Content);
			
			_log.Warning($"Auto post on Twitter, response: {response}");
		}

	}
}

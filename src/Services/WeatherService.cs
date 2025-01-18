using System;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace App {
	public class WeatherService {
		readonly DiscordSocketClient _discord;
		readonly IConfigurationRoot _config;

		
		
		
		public WeatherService(DiscordSocketClient discord, IConfigurationRoot config) {
			_discord = discord;
			_config = config;
		}
		
	}
}
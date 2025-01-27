using App.Attributes;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Services;

[Service(ServiceLifetime.Singleton)]
public class WeatherService {
	readonly DiscordSocketClient _discord;
	readonly IConfigurationRoot _config;

	public WeatherService(DiscordSocketClient discord, IConfigurationRoot config) {
		_discord = discord;
		_config = config;
	}
		
}
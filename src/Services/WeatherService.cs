using App.Attributes;
using App.Models;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using System.Web;
using System.Net.Http;

namespace App.Services;

[Service(ServiceLifetime.Singleton)]
public class WeatherService {
	readonly DiscordSocketClient _discord;
	readonly BotSettings _settings;
	readonly IHttpClientFactory _httpClientFactory;
	const string PATH = "WeatherInfo/";
	readonly TimeSpan MAX_CACHE_AGE = TimeSpan.FromMinutes(30);

	public WeatherService(DiscordSocketClient discord, IOptionsSnapshot<BotSettings> settings, IHttpClientFactory httpClientFactory) {
		_discord = discord;
		_settings = settings.Value;
		_httpClientFactory = httpClientFactory;
	}

	public async Task<WeatherResponse?> GetWeatherAsync(string location) {
		location = ChatService.RemoveDiacritics(location.ToLower());

		// check cache
		var weatherJson = JsonCache.LoadFromJson<WeatherResponse>($"{PATH}{location}", MAX_CACHE_AGE);
		if (weatherJson != null) return weatherJson;

		var locationEncoded = HttpUtility.UrlEncode(location);
		var apiKey = _settings.WeatherApiKey;

		var restClient = new RestClient(_httpClientFactory.CreateClient());
		var response = await restClient.ExecuteAsync(new RestRequest($"https://api.openweathermap.org/data/2.5/weather?q={locationEncoded}&appid={apiKey}", Method.Get));

		if (!string.IsNullOrEmpty(response.ErrorMessage)) {
			Console.WriteLine($"Error trying to get weather for '{locationEncoded}': {response.ErrorMessage}");
			return null;
		}
		if (string.IsNullOrEmpty(response.Content)) return null;

		weatherJson = JsonConvert.DeserializeObject<WeatherResponse>(response.Content, JsonCache.DefaultSerializer);
		if (weatherJson == null || weatherJson.ErrorCode != 200) {
			Console.WriteLine($"Error trying to parse weather json for {location}! Content:\n{response.Content}");
			return null;
		}

		weatherJson.CacheTime = (DateTime.UtcNow - TimeSpan.FromHours(3)).ToString("hh:mm:ss tt");
		JsonCache.SaveToJson($"{PATH}{location}", weatherJson);

		return weatherJson;
	}
		
}
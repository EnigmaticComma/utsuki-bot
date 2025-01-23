using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using App.Models;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using App.Extensions;
using RestSharp;

namespace App.Modules {
	public class WeatherModule : ModuleBase<SocketCommandContext> {

		private const string PATH = "WeatherInfo/";
		private readonly WeatherService _service;
		private readonly DiscordSocketClient client;
		private readonly IConfigurationRoot _config;
		private readonly TimeSpan MAX_CACHE_AGE = TimeSpan.FromMinutes(30);

		public WeatherModule(DiscordSocketClient client, IConfigurationRoot config, WeatherService service) {
			_service = service;
			this.client = client;
			_config = config;

			client.Ready += Client_Ready;
			client.SlashCommandExecuted += SlashCommandHandler;
		}

		private async Task Client_Ready() {
			Console.WriteLine("Registering guild commnads");
			var mainGuild = client.GetGuild(264800866169651203);

			var command_clima = new SlashCommandBuilder {
				Name = "clima",
				Description = "Ver o clima de uma região",
				IsDMEnabled = false,
				IsNsfw = false
			};

			try {
				await mainGuild.CreateApplicationCommandAsync(command_clima.Build());
				await client.CreateGlobalApplicationCommandAsync(command_clima.Build());
			}
			catch(HttpException exception) {
				// If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
				var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

				// You can send this error somewhere or just print it to the console, for this example we're just going to print it.
				Console.WriteLine(json);
			}
		}

		private async Task SlashCommandHandler(SocketSlashCommand command) {
			switch (command.Data.Name) {
				case "clima":
					await ShowWeather(command.Data.Options.First().Value.ToString());
					break;
			}
		}

		public async Task ShowWeather(params string[] locationStrings) {
			if (locationStrings.Length <= 0) return;
			var location = locationStrings.CJoin();
			if (string.IsNullOrEmpty(location)) return;
			location = ChatService.RemoveDiacritics(location.ToLower());

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			// check cache
			var weatherJson = JsonCache.LoadFromJson<WeatherResponse>($"{PATH}{location}", MAX_CACHE_AGE);
			if (weatherJson == null) {
				
				var locationEncoded = HttpUtility.UrlEncode(location);
				var apiKey = _config["API_KEY_WEATHER"];
				
				// api.openweathermap.org/data/2.5/weather?q={city name}&appid={API key}
				var client = new RestClient();
				var timeline = await client.ExecuteAsync(new RestRequest($"https://api.openweathermap.org/data/2.5/weather?q={locationEncoded}&appid={apiKey}", Method.Get));

				if (!string.IsNullOrEmpty(timeline.ErrorMessage)) {
					Console.WriteLine($"Error trying to get weather for '{locationEncoded}': {timeline.ErrorMessage}");
					return;
				}
				if (string.IsNullOrEmpty(timeline.Content)) return;

				weatherJson = JsonConvert.DeserializeObject<WeatherResponse>(timeline.Content, JsonCache.DefaultSerializer);
				if (weatherJson == null || weatherJson.ErrorCode != 200) {
					Console.WriteLine($"Error trying to parse weather json for {location}! timeline.Content:\n{timeline.Content}");
					return;
				}

				weatherJson.CacheTime = (DateTime.UtcNow - TimeSpan.FromHours(3)).ToString("hh:mm:ss tt");
				
				JsonCache.SaveToJson($"{PATH}{location}", weatherJson);
			}

			var feelsLike = weatherJson.MainWeatherTemperature.FeelsLikeCelsius;
			
			var embed = new EmbedBuilder();

			embed.Title = $"{feelsLike:0} °C";
			embed.Description = $"Sensação térmica em {weatherJson.LocalName}";

			if (!string.IsNullOrEmpty(weatherJson.CacheTime)) {
				embed.Footer = new EmbedFooterBuilder {
					Text = $"Atualizado as {weatherJson.CacheTime}"
				};
			}

			// get icon
			try {
				var iconCode = weatherJson.WeatherInfoModel[0].Icon;
				embed.ThumbnailUrl = $"http://openweathermap.org/img/w/{iconCode}.png";

			} catch (Exception e) {
				Console.WriteLine($"Error trying to set icon from weather {location}:\n{e}");
			}
			
			// get humildade
			try {
				var value = weatherJson.MainWeatherTemperature.Humidity;
				embed.AddField(
					new EmbedFieldBuilder {
						Name = $"{value}%",
						Value = "Humildade",
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set humidity: {e}");
			}

			// temperature
			float temperature = 0;
			try {
				temperature = weatherJson.MainWeatherTemperature.TemperatureCelsius;
				embed.AddField(
					new EmbedFieldBuilder {
						Name = $"{temperature:0} °C",
						Value = "Temperatura",
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set sensation: {e}");
			}
			
			// get wind
			try {
				var value = weatherJson.Wind.Speed * 3.6f; // mp/s to km/h
				embed.AddField(
					new EmbedFieldBuilder {
						Name = $"{value:0} (km/h)",
						Value = "Ventos",
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set wind: {e}");
			}

			// get weather name
			try {
				var value = weatherJson.WeatherInfoModel[0].Main;
				var description = weatherJson.WeatherInfoModel[0].Description;
				embed.AddField(
					new EmbedFieldBuilder {
						Name = value,
						Value = description,
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set weather name and description field: {e}");
			}
			
			await ReplyAsync(GetWeatherVerbalStatus((int)feelsLike), false, embed.Build());
		}

		private string GetWeatherVerbalStatus(int celsiusTemp) {
			if (celsiusTemp >= 45) {
				return "+ quente q o cu do sabs kkk";
			}
			if (celsiusTemp >= 40) {
				return "40 graus que tipo de Nordeste eh esse?";
			}
			if (celsiusTemp >= 35) {
				return "ta quente pracaralho";
			}
			if (celsiusTemp >= 30) {
				return "muito quente se eh loco";
			}
			if (celsiusTemp >= 23) {
				return "quente";
			}
			if (celsiusTemp >= 20) {
				return "maravilha";
			}
			if (celsiusTemp >= 15) {
				return "friozin";
			}
			if (celsiusTemp >= 10) {
				return "sul só pode";
			}
			if (celsiusTemp >= 5) {
				return "friodaporra";
			}
			if (celsiusTemp >= -4) {
				return "temperatura ideal de sulista";
			}
			if (celsiusTemp < -4) {
				return "boa bot, ta mais frio q dentro de uma geladeira kkk";
			}
			
			return string.Empty;
		}
		
	}
}
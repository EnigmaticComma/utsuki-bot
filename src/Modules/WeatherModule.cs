using System.Globalization;
using App.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using App.Extensions;
using App.Services;

namespace App.Modules {
	public class WeatherModule : InteractionModuleBase<SocketInteractionContext> {
		readonly WeatherService _service;

		public WeatherModule(WeatherService service) {
			_service = service;
		}

		[SlashCommand("clima", "Ver o clima de uma região")]
		public async Task ShowWeather(string location) {
			if (string.IsNullOrEmpty(location)) return;

			var weatherJson = await _service.GetWeatherAsync(location);
			if (weatherJson == null) {
				await RespondAsync("Não consegui encontrar informações para essa localização.", ephemeral: true);
				return;
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
			} catch { /* ignore */ }
			
			// get humidity
			try {
				var value = weatherJson.MainWeatherTemperature.Humidity;
				embed.AddField("Humildade", $"{value}%", true);
			} catch { /* ignore */ }

			// temperature
			try {
				var temperature = weatherJson.MainWeatherTemperature.TemperatureCelsius;
				embed.AddField("Temperatura", $"{temperature:0} °C", true);
			} catch { /* ignore */ }
			
			// get wind
			try {
				var value = weatherJson.Wind.Speed * 3.6f; // mp/s to km/h
				embed.AddField("Ventos", $"{value:0} (km/h)", true);
			} catch { /* ignore */ }

			// get weather name
			try {
				var value = weatherJson.WeatherInfoModel[0].Main;
				var description = weatherJson.WeatherInfoModel[0].Description;
				embed.AddField(value, description, true);
			} catch { /* ignore */ }
			
			await RespondAsync(GetWeatherVerbalStatus((int)feelsLike), embed: embed.Build());
		}

		string GetWeatherVerbalStatus(int celsiusTemp) {
			if (celsiusTemp >= 45) return "+ quente q o cu do sabs kkk";
			if (celsiusTemp >= 40) return "40 graus que tipo de Nordeste eh esse?";
			if (celsiusTemp >= 35) return "ta quente pracaralho";
			if (celsiusTemp >= 30) return "muito quente se eh loco";
			if (celsiusTemp >= 23) return "quente";
			if (celsiusTemp >= 20) return "maravilha";
			if (celsiusTemp >= 15) return "friozin";
			if (celsiusTemp >= 10) return "sul só pode";
			if (celsiusTemp >= 5) return "friodaporra";
			if (celsiusTemp >= -4) return "temperatura ideal de sulista";
			if (celsiusTemp < -4) return "boa bot, ta mais frio q dentro de uma geladeira kkk";
			
			return string.Empty;
		}
	}
}

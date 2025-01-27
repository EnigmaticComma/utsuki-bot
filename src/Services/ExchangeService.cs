using System.Globalization;
using System.Reactive.Disposables;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace App.Services;

public class ExchangeService : IDisposable {
	readonly DiscordSocketClient _discord;
	readonly IConfigurationRoot _config;
	CompositeDisposable _disposable;


	const string JPATH_EXCHANGEINFO = "ExchangeInfo";
	const string JKEY_USD_BRL = "USD_BRL";

	#region <<---------- Initializers ---------->>

	public ExchangeService(DiscordSocketClient discord, IConfigurationRoot config) {
		_disposable?.Dispose();
		_disposable = new CompositeDisposable();

		// dependecies injected
		_discord = discord;
		_config = config;

		_discord.MessageReceived += DiscordOnMessageReceived;
	}

	#endregion <<---------- Initializers ---------->>


	async Task DiscordOnMessageReceived(SocketMessage socketMessage) {
		if (!(socketMessage is SocketUserMessage socketUserMessage)) return;
		if (socketUserMessage.Channel == null) return;
		var msg = socketUserMessage.Resolve().ToLower();
		if (string.IsNullOrEmpty(msg)) return;

		// check for msg about dolar
		if (msg.Length == 1) {
			if (msg != "$") return;
		}
		else {
			if (!msg.Contains("dolar")) return;
		}

		// thread culture
		var cultureInfo = new CultureInfo("en-us");
		Thread.CurrentThread.CurrentCulture = cultureInfo;
		Thread.CurrentThread.CurrentUICulture = cultureInfo;

		// get last exchange json
		var exchangeJson = JsonCache.LoadFromJson<JObject>(JPATH_EXCHANGEINFO, TimeSpan.FromMinutes(15));

		EmbedBuilder embed = null;

		if (exchangeJson == null) {
			var apiKey = _config["apikey-freecurrencyconverter"];

			// Example:
			// https://free.currconv.com/api/v7/convert?q=USD_BRL&compact=ultra&apiKey=82e456034f1bb5418116
			var client = new RestClient();
			var timeline = await client.ExecuteAsync(new RestRequest($"https://free.currconv.com/api/v7/convert?q=USD_BRL&compact=ultra&apiKey={apiKey}", Method.Get));


			if (!timeline.IsSuccessful || !string.IsNullOrEmpty(timeline.ErrorMessage)) {
				Console.WriteLine($"Error trying to get exchangeJson: {timeline.ErrorMessage}");
				embed = new EmbedBuilder {
					Title = "Erro",
					Description = "O serviço de cotação de dolar que eu uso não ta disponível",
					Color = Color.Red,
					Footer = new EmbedFooterBuilder {
						Text = timeline.StatusDescription
					}
				};
				await socketUserMessage.ReplyAsync(string.Empty, false, embed.Build());
				return;
			}

			exchangeJson = JObject.Parse(timeline.Content);
			if (!exchangeJson.HasValues) {
				Console.WriteLine($"Error trying to parse exchangeJson! timeline.Content:\n{timeline.Content}");
				return;
			}

			exchangeJson["cacheTime"] = (DateTime.UtcNow - TimeSpan.FromHours(3)).ToString("hh:mm:ss tt");

			JsonCache.SaveToJson(JPATH_EXCHANGEINFO, exchangeJson);
		}

		var currencyValue = exchangeJson[JKEY_USD_BRL]?.Value<float>();

		embed = new EmbedBuilder {
			Title = currencyValue.Value.ToString("C2", cultureInfo),
			Description = "Cotação do dolar",
			Color = Color.DarkGreen
		};

		var cacheTimeStr = exchangeJson["cacheTime"]?.Value<string>();
		if (!string.IsNullOrEmpty(cacheTimeStr)) {
			embed.Footer = new EmbedFooterBuilder {
				Text = $"Atualizado as {cacheTimeStr}"
			};
		}

		var channel = socketUserMessage.Channel;
		if (socketUserMessage.Channel == null) return;

		await channel.SendMessageAsync(MentionUtils.MentionUser(socketUserMessage.Author.Id), false, embed.Build());
	}



	#region <<---------- IDisposable ---------->>

	public void Dispose() {
		_disposable?.Dispose();
		_disposable = new CompositeDisposable();

		_discord.MessageReceived -= DiscordOnMessageReceived;
	}

	#endregion <<---------- IDisposable ---------->>

}
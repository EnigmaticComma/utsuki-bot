using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace App.Services;

public class AIAnswerService
{
    readonly LoggingService _log;
    readonly IConfigurationRoot _config;
    string _instructions;

    public AIAnswerService(DiscordSocketClient discord, LoggingService loggingService, IConfigurationRoot config)
    {
        _log = loggingService;
        _config = config;
        discord.MessageReceived += OnMessageReceived;
        try {
            string filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            using var r = new StreamReader(filePath + "/resources/ggj_instructions.txt");
            _instructions = r.ReadToEnd();
        }
        catch (Exception e) {
            _log.Error(e.Message);
        }
        Console.WriteLine("AIAnswerService init!");
    }

    async Task OnMessageReceived(SocketMessage socketMessage)
    {
        if (socketMessage.Author.IsBot) return;
        if(socketMessage is not SocketUserMessage userMessage) return;
        if(userMessage.Channel is not SocketTextChannel textChannel) return;
        if(textChannel.Guild.Id != 1328551591241846907) return;
        if(userMessage.Content == null || userMessage.Content.Length <= 5 || userMessage.Content.Last() != '?') return;

        _log.Info("Trying to answer with AI");

        var endpoint = _config["AI_ENDPOINT"];
        if(endpoint == null) {
            _log.Info("No AI endpoint to answer message.");
            return;
        }

        var requestUrl = endpoint + "/v1/chat/completions";

        var question = userMessage.Content;

        // construct request to AI API without auth:
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post,requestUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(new {
            stream = false,
            messages = new[] {
                new {
                    role = "system",
                    content = _instructions
                },
                new {
                    role = "user",
                    content = question
                }!,
            }
        }),Encoding.UTF8,"application/json");

        _log.Info($"Preparing request to: '{requestUrl}' with content: {request.Content}");
        var response = await client.SendAsync(request);
        if(!response.IsSuccessStatusCode) {
            _log.Error($"Failed to get AI response: {response.StatusCode}");
            return;
        }

        _log.Info($"Received response");

        var responseContent = await response.Content.ReadAsStringAsync();
        const string InvalidStart = "data: ";
        if(responseContent.StartsWith(InvalidStart)) responseContent = responseContent.Substring(InvalidStart.Length);

        // deserialize generic json getting only the response text:
        _log.Info($"Response content: {responseContent}");

        dynamic responseJson = JObject.Parse(responseContent);
        string responseText = responseJson["choices"][0]["message"]["content"];

        if(responseText == "?????") {
            await userMessage.AddReactionAsync(new Emoji("❔"));
            return;
        }

        _log.Info($"AI Answer: {responseText}");

        string? title;
        string? description;

        using (var reader = new StringReader(responseText))
        {
            title = await reader.ReadLineAsync();
            description = await reader.ReadToEndAsync();
        }

        var embed = new EmbedBuilder()
            .WithDescription(string.IsNullOrWhiteSpace(description) ? title : description)
            .WithFooter(new EmbedFooterBuilder {
                Text = "Resposta por IA experimental",
                IconUrl = "https://raw.githubusercontent.com/EnigmaticComma/enigmaticcomma.github.io/refs/heads/main/favicon-32x32.png"
            })
            .WithColor(new Color(0x2c5d87));

        if(!string.IsNullOrEmpty(title)) embed.WithTitle(title);

        try {
            var thread = await textChannel.CreateThreadAsync(userMessage.CleanContent,
                ThreadType.PublicThread,
                ThreadArchiveDuration.ThreeDays
            );
            await thread.SendMessageAsync("",false,embed.Build(), null, AllowedMentions.None, userMessage.Reference);
        }
        catch (Exception e) {
            Console.WriteLine(e);
            await textChannel.SendMessageAsync("",false,embed.Build(), null, AllowedMentions.None, userMessage.Reference);
        }

    }
}
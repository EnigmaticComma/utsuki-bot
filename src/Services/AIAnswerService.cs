using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace App.Services;

public class AIAnswerService
{
    readonly LoggingService _log;
    readonly IConfigurationRoot _config;


    public AIAnswerService(DiscordSocketClient discord, LoggingService loggingService, IConfigurationRoot config)
    {
        _log = loggingService;
        _config = config;
        discord.MessageReceived += OnMessageReceived;
    }

    async Task OnMessageReceived(SocketMessage socketMessage)
    {
        _log.Info($"AI message received: {socketMessage.Content}");
        if(socketMessage is not SocketUserMessage userMessage) return;
        if(userMessage.Source != MessageSource.User) return;
        if(userMessage.Content == null || userMessage.Content.Length <= 5 || userMessage.Content.Last() != '?') return;

        _log.Info("Trying to answer with AI");

        var endpoint = _config["AI_ENDPOINT"];
        if(endpoint == null) {
            _log.Info("No AI endpoint to answer message.");
            return;
        }

        var requestUrl = endpoint + "/v1/chat/completions";

        var question = userMessage.Content;
        var instructions = _config["EVENT_INSTRUCTIONS"];

        // construct request to AI API without auth:
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post,requestUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(new {
            stream = "false",
            messages = new[] {
                new {
                    role = "system",
                    content = instructions
                },
                new {
                    role = "user",
                    content = question
                }!,
            }
        }),Encoding.UTF8,"application/json");

        _log.Info($"Preparing request to: '{requestUrl}' with instructions: {instructions}");
        var response = await client.SendAsync(request);
        if(!response.IsSuccessStatusCode) {
            _log.Error($"Failed to get AI response: {response.StatusCode}");
            return;
        }

        _log.Info($"Received response");
        var responseContent = await response.Content.ReadAsStringAsync();
        // deserialize generic json getting only the response text:
        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var responseText = responseJson.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        _log.Info($"AI Answer: {responseText}");

    }
}
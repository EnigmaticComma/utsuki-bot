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


    public AIAnswerService(DiscordSocketClient discord, LoggingService loggingService, IConfigurationRoot config)
    {
        _log = loggingService;
        _config = config;
        discord.MessageReceived += OnMessageReceived;
        Console.WriteLine("AIAnswerService init!");
    }

    async Task OnMessageReceived(SocketMessage socketMessage)
    {
        if (socketMessage.Author.IsBot) return;
        if(socketMessage is not SocketUserMessage userMessage) return;
        if(userMessage.Channel is not SocketTextChannel textChannel) return;
        if(userMessage.Content == null || userMessage.Content.Length <= 5 || userMessage.Content.Last() != '?') return;

        _log.Info("Trying to answer with AI");

        var endpoint = _config["AI_ENDPOINT"];
        if(endpoint == null) {
            _log.Info("No AI endpoint to answer message.");
            return;
        }

        bool insideThread = false;

        foreach (var t in await textChannel.GetActiveThreadsAsync()) {
            if(t == null) continue;
            if(t.ParentChannelId == textChannel.Id) {
                insideThread = true;
                break;
            }
        }

        var requestUrl = endpoint + "/v1/chat/completions";

        var question = userMessage.Content;
        var instructions = _config["EVENT_INSTRUCTIONS"];

        // construct request to AI API without auth:
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post,requestUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(new {
            stream = false,
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

        var embed = new EmbedBuilder()
            .WithDescription(responseText)
            .WithFooter("Resposta por IA experimental")
            .WithColor(Color.Blue);

        if(!insideThread) {
            var thread = await textChannel.CreateThreadAsync(userMessage.CleanContent,
                ThreadType.PublicThread,
                ThreadArchiveDuration.ThreeDays
            );
            await thread.SendMessageAsync("",false,embed.Build(), null, AllowedMentions.None, userMessage.Reference);
        }
        else {
            await textChannel.SendMessageAsync("",false,embed.Build(), null, AllowedMentions.None, userMessage.Reference);
        }

    }
}
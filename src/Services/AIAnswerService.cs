using System.Text;
using System.Text.Json;
using App.Attributes;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace App.Services;

[Service]
public class AIAnswerService
{
    readonly LoggingService _log;
    readonly IConfigurationRoot _config;
    string _instructions;
    HttpClient _httpClient;

    public AIAnswerService(DiscordSocketClient discord, LoggingService loggingService, IConfigurationRoot config)
    {
        _log = loggingService;
        _config = config;
        _httpClient = new HttpClient();
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
        if(userMessage.Channel is not ITextChannel textChannel) return;
        if(userMessage.ReferencedMessage != null) return;
        if(textChannel.Guild.Id != 1333473843674878015) return;
        
        // Basic filter to avoid processing everything, but allow AI to decide relevance
        if(string.IsNullOrWhiteSpace(userMessage.Content) || userMessage.Content.Length < 3) return;

        bool isThread = textChannel is IThreadChannel;

        if (!isThread)
        {
            // Flow for new question (creates thread)
            bool isRelevant = await CheckIfRelevant(userMessage.Content);
            if (!isRelevant) return;

            _log.Info("Message deemed relevant by AI. Proceeding to answer.");
            await userMessage.AddReactionAsync(new Emoji("👀"));

            // Generate Thread Title
            string threadTitle = await GenerateThreadTitle(userMessage.Content);
            if (string.IsNullOrWhiteSpace(threadTitle)) threadTitle = "Dúvida GGJ";

            // Create Thread
            IThreadChannel? thread = null;
            try {
                if (textChannel is SocketTextChannel socketTextChannel)
                {
                    thread = await socketTextChannel.CreateThreadAsync(threadTitle, ThreadType.PublicThread, ThreadArchiveDuration.ThreeDays, userMessage);
                }
            }
            catch (Exception e) {
                _log.Error($"Failed to create thread: {e.Message}");
            }

            // Generate Answer
            string answer = await GenerateAnswer(userMessage.Content, null, threadTitle);
            if (string.IsNullOrWhiteSpace(answer)) {
                 _log.Error("AI failed to generate an answer.");
                 return;
            }

            var embed = new EmbedBuilder()
                .WithDescription(answer)
                .WithFooter(new EmbedFooterBuilder {
                    Text = "Resposta por IA experimental",
                    IconUrl = "https://raw.githubusercontent.com/EnigmaticComma/enigmaticcomma.github.io/refs/heads/main/favicon-32x32.png"
                })
                .WithColor(new Color(0x2c5d87));

            if (thread != null) {
                 await thread.SendMessageAsync(string.Empty, false, embed.Build());
            } else {
                 await userMessage.ReplyAsync(string.Empty, false, embed.Build());
            }
        }
        else
        {
            // Flow for conversation within existing thread
            var thread = (IThreadChannel)textChannel;
            
            // Fetch thread history
            var history = await thread.GetMessagesAsync(15).FlattenAsync();
            
            // Generate answer with context
            string answer = await GenerateAnswer(userMessage.Content, history, thread.Name);
            if (string.IsNullOrWhiteSpace(answer)) return;

            var embed = new EmbedBuilder()
                .WithDescription(answer)
                .WithFooter(new EmbedFooterBuilder {
                    Text = "Resposta por IA experimental",
                    IconUrl = "https://raw.githubusercontent.com/EnigmaticComma/enigmaticcomma.github.io/refs/heads/main/favicon-32x32.png"
                })
                .WithColor(new Color(0x2c5d87));

            await thread.SendMessageAsync(string.Empty, false, embed.Build());
        }
    }

    private async Task<string> CallAI(object messagesPayload, double temperature = 0.7)
    {
         var endpoint = _config["AI_ENDPOINT"];
        if(string.IsNullOrEmpty(endpoint)) {
            _log.Error("No AI endpoint configured.");
            return null;
        }
        var requestUrl = endpoint + "/v1/chat/completions";

        var payload = new {
            stream = false,
            model = _config["AI_MODEL"],
            temperature = temperature,
            messages = messagesPayload
        };

        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var token = _config["AI_TOKEN"];
        if(!string.IsNullOrEmpty(token)) {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        try {
             var response = await _httpClient.SendAsync(request);
             if(!response.IsSuccessStatusCode) {
                 _log.Error($"AI Request failed: {response.StatusCode}");
                 return null;
             }
             var responseContent = await response.Content.ReadAsStringAsync();
             // Simple cleanup if needed, though usually standard API doesn't send "data: " prefix unless stream=true
             const string InvalidStart = "data: ";
            if(responseContent.StartsWith(InvalidStart)) responseContent = responseContent.Substring(InvalidStart.Length);

            dynamic responseJson = JObject.Parse(responseContent);
            return responseJson["choices"][0]["message"]["content"];

        } catch (Exception ex) {
            _log.Error($"Exception during AI call: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> CheckIfRelevant(string userMessage)
    {
        var messages = new[] {
            new { role = "system", content = "Você é um classificador de mensagens para um evento de Game Jam (Global Game Jam). Sua única tarefa é dizer se a mensagem do usuário é uma DÚVIDA PERTINENTE ao evento ou não. Responda apenas com 'TRUE' se for uma dúvida sobre o evento, regras, horários, locais, etc. Responda 'FALSE' se for conversa fiada, 'oi', 'bom dia', spam ou não relacionado. A dúvida não precisa ser formal, apenas ter intenção de saber algo sobre o evento." },
            new { role = "user", content = userMessage }
        };

        // Retry logic for consistency check could go here, but for now let's trust a low temp call.
        // The user asked for "majority vote" check. Let's implement a simple 3-check loop.
        
        int trueCount = 0;
        int falseCount = 0;

        for(int i=0; i<3; i++) {
            string response = await CallAI(messages, 0.1); // Low temp for determinism
            if (string.IsNullOrWhiteSpace(response)) continue;
            
            response = response.Trim().ToUpper();
            if (response.Contains("TRUE")) trueCount++;
            else if (response.Contains("FALSE")) falseCount++;
        }

        return trueCount > falseCount;
    }

    private async Task<string> GenerateThreadTitle(string userMessage)
    {
         var messages = new[] {
            new { role = "system", content = "Analise a pergunta do usuário e crie um título curto e descritivo para uma thread de suporte no Discord (máximo 50 caracteres). Exemplo: 'Horário de Início', 'Dúvida sobre Regras', 'Local do Evento'. Não use pontuação final." },
            new { role = "user", content = userMessage }
        };
        string title = await CallAI(messages, 0.5);
        if(title != null) {
            title = title.Trim().Replace("\"", "").Replace("'", "");
            if(title.Length > 90) title = title.Substring(0, 90) + "..."; // Safety cap below 100
        }
        return title;
    }

    private async Task<string> GenerateAnswer(string userMessage, IEnumerable<IMessage>? context, string? threadTitle)
    {
        var messages = new List<object>();
        string systemPrompt = _instructions + "\n\nIMPORTANTE: Responda a dúvida do usuário com base APENAS nas informações acima. Seja direto e prestativo. Não comece com 'Olá' nem termine com 'Atenciosamente', apenas dê a informação. Se a informação não estiver no texto, diga que não sabe e sugere falar com um organizador. Se for uma continuação de conversa, mantenha o contexto.";
        
        if (!string.IsNullOrEmpty(threadTitle))
        {
            systemPrompt += $"\nO tópico desta conversa (título da thread) é: '{threadTitle}'.";
        }

        messages.Add(new { role = "system", content = systemPrompt });

        if (context != null)
        {
            // Discord provides messages from newest to oldest. We need them chronological.
            var sortedContext = context.OrderBy(m => m.Timestamp);
            foreach (var msg in sortedContext)
            {
                // Ignore the current message as it will be added at the end
                if (msg.Content == userMessage) continue;

                string role = msg.Author.IsBot ? "assistant" : "user";
                string content = msg.Content;

                // Handle bot messages that might be embeds (our answers)
                if (msg.Author.IsBot && string.IsNullOrWhiteSpace(content) && msg.Embeds.Count > 0)
                {
                    content = msg.Embeds.First().Description;
                }

                if (!string.IsNullOrWhiteSpace(content))
                {
                    messages.Add(new { role = role, content = content });
                }
            }
        }

        messages.Add(new { role = "user", content = userMessage });

        return await CallAI(messages.ToArray(), 0.7);
    }
}


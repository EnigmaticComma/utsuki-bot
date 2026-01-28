using System.Text;
using System.Text.Json;
using App.Attributes;
using App.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace App.Services;

[Service]
public class AIAnswerService
{
    readonly LoggingService _log;
    readonly BotSettings _settings;
    readonly IHttpClientFactory _httpClientFactory;
    string _instructions = string.Empty;
    DateTime _lastInstructionsUpdate = DateTime.MinValue;

    const string InstructionsUrl = "https://raw.githubusercontent.com/EnigmaticComma/utsuki-bot/master/resources/ggj_instructions.txt";

    public AIAnswerService(DiscordSocketClient discord, LoggingService loggingService, IOptionsSnapshot<BotSettings> settings, IHttpClientFactory httpClientFactory)
    {
        _log = loggingService;
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        discord.MessageReceived += OnMessageReceived;
        Console.WriteLine("AIAnswerService init!");
    }

    private async Task<string> GetInstructionsAsync()
    {
        if (DateTime.UtcNow - _lastInstructionsUpdate < TimeSpan.FromHours(1) && !string.IsNullOrEmpty(_instructions))
        {
            return _instructions;
        }

        var httpClient = _httpClientFactory.CreateClient();
        try
        {
            _log.Info("Fetching instructions from GitHub...");
            var response = await httpClient.GetAsync(InstructionsUrl);
            if (response.IsSuccessStatusCode)
            {
                _instructions = await response.Content.ReadAsStringAsync();
                _lastInstructionsUpdate = DateTime.UtcNow;
                _log.Info("Instructions updated from GitHub.");
                return _instructions;
            }
            _log.Warning($"Failed to fetch instructions from GitHub: {response.StatusCode}. Falling back to local file.");
        }
        catch (Exception ex)
        {
            _log.Error($"Exception while fetching remote instructions: {ex.Message}. Falling back to local file.");
        }

        // Fallback to local file
        try
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "ggj_instructions.txt");
            if (File.Exists(filePath))
            {
                _instructions = await File.ReadAllTextAsync(filePath);
                _lastInstructionsUpdate = DateTime.UtcNow; // Still count as an update to avoid spamming the remote if it's down
                _log.Info("Instructions loaded from local fallback.");
            }
            else
            {
                _log.Error($"Local instructions file not found for fallback! Path: {filePath}");
            }
        }
        catch (Exception e)
        {
            _log.Error($"Failed to load local instructions fallback: {e.Message}");
        }

        return _instructions;
    }

    async Task OnMessageReceived(SocketMessage socketMessage)
    {
        if (socketMessage.Author.IsBot) return;
        if(socketMessage is not SocketUserMessage userMessage) return;
        if(userMessage.Channel is not ITextChannel textChannel) return;
        if(userMessage.ReferencedMessage != null) return;
        if(textChannel.Guild.Id != _settings.GgjGuildId) return;
        
        // Basic filter to avoid processing everything, but allow AI to decide relevance
        if(string.IsNullOrWhiteSpace(userMessage.Content) || userMessage.Content.Length < 3) return;
        
        // Only process messages that contain a question mark
        if(!userMessage.Content.Contains('?')) return;

        bool isThread = textChannel is IThreadChannel;

        if (!isThread)
        {
            // Flow for new question (creates thread)
            bool isRelevant = await CheckIfRelevant(userMessage.Content, false);
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

            // Only respond inside support threads if it's a question/relevant
            bool isRelevant = await CheckIfRelevant(userMessage.Content, true);
            if (!isRelevant) return;
            
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
        var endpoint = _settings.AiEndpoint;
        if(string.IsNullOrEmpty(endpoint)) {
            _log.Error("No AI endpoint configured.");
            return null;
        }
        var requestUrl = endpoint + "/v1/chat/completions";

        var payload = new {
            stream = false,
            model = _settings.AiModel,
            temperature = temperature,
            messages = messagesPayload
        };

        var httpClient = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var token = _settings.AiToken;
        if(!string.IsNullOrEmpty(token)) {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        try {
             var response = await httpClient.SendAsync(request);
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

    private async Task<bool> CheckIfRelevant(string userMessage, bool isInsideThread)
    {
        string systemPrompt = isInsideThread
            ? "Você está em uma thread de suporte da Global Game Jam. Sua única tarefa é identificar se a mensagem atual do usuário é uma NOVA PERGUNTA QUE REQUER UMA RESPOSTA TÉCNICA OU INFORMATIVA. Responda 'TRUE' APENAS se o usuário estiver pedindo uma informação nova, ajuda ou esclarecimento. Responda 'FALSE' para: perguntas retóricas (ex: 'Sério?', 'Ah é?'); confirmações ou agradecimentos (ex: 'Entendi, beleza?', 'Tudo certo?'); comentários. Seja RÍGIDO. Na dúvida, responda 'FALSE'."
            : "Você é um classificador para o evento Global Game Jam. Sua tarefa é identificar se a mensagem é uma PERGUNTA diretamente RELACIONADA ao evento. Responda 'TRUE' apenas se for uma dúvida sobre o evento (horários, regras, local, etc). Responda 'FALSE' se for apenas conversa, comentário sobre o evento sem ser pergunta, saudação, ou assunto não relacionado.";

        var messages = new[] {
            new { role = "system", content = systemPrompt + "\nResponda APENAS 'TRUE' ou 'FALSE'." },
            new { role = "user", content = userMessage }
        };

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
        var instructions = await GetInstructionsAsync();
        var messages = new List<object>();
        string systemPrompt = instructions + "\n\nIMPORTANTE: Responda a dúvida do usuário com base APENAS nas informações acima. Seja direto e prestativo. Não comece com 'Olá' nem termine com 'Atenciosamente', apenas dê a informação. Se a informação não estiver no texto, diga que não sabe e sugere falar com um organizador. Se for uma continuação de conversa, mantenha o contexto.";
        
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


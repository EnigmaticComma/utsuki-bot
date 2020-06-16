using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace NyuBot {
	public class ChatService {
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;

		public ChatService(DiscordSocketClient discord, CommandService commands) {
			_commands = commands;
			_discord = discord;

			// Hook MessageReceived so we can process each message to see
			// if it qualifies as a command.
			_discord.MessageReceived += MessageReceivedAsync;
		}

		public async Task MessageReceivedAsync(SocketMessage rawMessage) {
			if (!(rawMessage is SocketUserMessage userMessage)) return;
			if (userMessage.Source != MessageSource.User) return;
			if (string.IsNullOrEmpty(userMessage.Content)) return;

			#region Setup message string to read
			// Content of the message in lower case string.
			string messageString = rawMessage.Content.ToLower();

			messageString = RemoveDiacritics(messageString);

			messageString = messageString.Trim();

			// if the message is a question
			bool isQuestion = false;
			if (messageString.Contains('?')) {
				// Get rid of all ?
				messageString = messageString.Replace("?", "");
				isQuestion = true;
			}
			bool userSaidHerName = false;

			// if user sayd her name
			if (HasAtLeastOneWord(messageString, new[] {"nyu", "nuy"})) {
				userSaidHerName = true;
				messageString = RemoveBotNameFromMessage(messageString);
			}
			else if (rawMessage.MentionedUsers.Contains(_discord.CurrentUser)) {
				// remove the mention string from text
				messageString = messageString.Replace(_discord.CurrentUser.Mention, "");
				userSaidHerName = true;
			}

			// remove double and tripple spaces
			messageString = messageString.Replace("  ", " ").Replace("   ", " ");

			// See if message is empty now
			if (messageString.Length <= 0) {
				if (userSaidHerName) {
					await userMessage.AddReactionAsync(new Emoji(":question:"));
				}
				return;
			}
			#endregion

			#region Fast Answers
			// Fast Tests
			if (messageString == ("ping")) {
				await rawMessage.Channel.SendMessageAsync("pong");
				return;
			}
			if (messageString == ("pong")) {
				await rawMessage.Channel.SendMessageAsync("ping");
				return;
			}

			if (messageString == ("marco")) {
				await rawMessage.Channel.SendMessageAsync("polo");
				return;
			}
			if (messageString == ("polo")) {
				await rawMessage.Channel.SendMessageAsync("marco");
				return;
			}

			if (messageString == ("dotto")) {
				await rawMessage.Channel.SendMessageAsync("Dotto. :musical_note:");
				return;
			}

			if (messageString == "❤" || messageString == ":heart:") {
				await rawMessage.Channel.SendMessageAsync("❤");
				return;
			}

			if (messageString == ":broken_heart:" || messageString == "💔") {
				await rawMessage.Channel.SendMessageAsync("❤");
				await userMessage.AddReactionAsync(new Emoji(":cry:"));
				return;
			}

			if (messageString == ("ne") || messageString == ("neh")) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Isso ai.", "Pode crê.", "Boto fé."}));
				return;
			}

			if (messageString == ("vlw") || messageString == ("valeu") || messageString == ("valew")) {
				await userMessage.AddReactionAsync(new Emoji(":wink:"));
				return;
			}

			// see if message is an Hi
			if (messageString == "oi"
				|| messageString == "ola"
				|| messageString == "hi"
				|| messageString == "hello"
				|| messageString == "coe"
				|| messageString == "ola pessoas"
				|| messageString == "coe rapaziada"
				|| messageString == "dae"
				|| messageString == "oi galera"
				|| messageString == "dae galera"
			) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Oi.", "Olá.", "Hello.", "Coé.", "Oin."}));
				return;
			}

			// see if message has an BYE
			if (messageString == "tchau"
				|| messageString == "xau"
				|| messageString == "tiau"
				|| messageString == "thau"
				|| messageString == "xau"
				|| messageString == "flw"
				|| messageString == "flws"
				|| messageString == "falou"
				|| messageString == "falous"
				|| messageString.Contains(" flw")
			) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Tchau.", "Xiau.", "Bye bye.", "Flw."}));
				return;
			}

			if (messageString.Contains("kk")) {
				if (Randomize().Next(100) < 20) {
					await userMessage.Channel.SendMessageAsync("kkk eae men.");
					return;
				}
			}
			#endregion

			#region Erase BotsCommands
			if (
				messageString.StartsWith(".") ||
				messageString.StartsWith(",") ||
				messageString.StartsWith(";;") ||
				messageString.StartsWith("!")
			) {
				await userMessage.AddReactionAsync(new Emoji(":x:"));
				await Task.Delay(1000 * 2); // 1 second
				await userMessage.DeleteAsync();
				return;
			}
			#endregion

			#region Nyu
			// check if user said nyu / nuy
			if (userSaidHerName) {
				if (HasAtLeastOneWord(messageString, new[] {"serve", "faz"})) {
					if (isQuestion) {
						await userMessage.Channel.SendMessageAsync("Sou um bot que responde diversas perguntas sobre assuntos comuns aqui no servidor. Com o tempo o Chris me atualiza com mais respostas e reações.");
						return;
					}
				}

				// Zueras
				if (messageString == ("vo ti cume")
					|| messageString == ("vo ti come")
					|| messageString == ("vou te come")
					|| messageString == ("quero te come")
					|| messageString == ("quero te pega")
				) {
					await userMessage.AddReactionAsync(new Emoji(":angry:"));
					await userMessage.Channel.SendMessageAsync("Não pode.");
					return;
				}

				// Praises
				if (messageString.Contains("gata")
					|| messageString.Contains("cremosa")
					|| messageString.Contains("gostosa")
					|| messageString.Contains("gatinha")
					|| messageString.Contains("linda")
					|| messageString.Contains("delicia")
					|| messageString.Contains("dlicia")
					|| messageString.Contains("dlcia")
					|| messageString == ("amo te")
					|| messageString == ("ti amu")
					|| messageString == ("ti amo")
					|| messageString == ("ti adoro")
					|| messageString == ("te adoro")
					|| messageString == ("te amo")
					|| messageString == ("obrigado")
					|| messageString == ("obrigada")
				) {
					await userMessage.AddReactionAsync(new Emoji(":heart:"));
					return;
				}

				if (messageString.Contains("manda nude")) {
					await userMessage.AddReactionAsync(new Emoji(":lennyFace:"));
					return;
				}
			}
			#endregion

			#region Animes
			// Check for `Boku no picu`
			if (messageString.Contains("boku no picu")
				|| messageString.Contains("boku no pico")
				|| messageString.Contains("boku no piku")
				|| messageString.Contains("boku no piku")
			) {
				await userMessage.Channel.SendMessageAsync(userMessage.Author.Mention + " Gay.");
				return;
			}
			#endregion

			#region Memes
			// Ahhh agora eu entendi
			if (messageString.EndsWith("agora eu entendi")) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Agora eu saqueeeeei!", "Agora tudo faz sentido!", "Eu estava cego agora estou enchergaaaando!", "Agora tudo vai mudar!", "Agora eu vou ficar de olhos abertos!"}));
				return;
			}

			#region Teu cu na minha mao
			// all possible answers
			if (messageString.Contains("mo vacilao") || messageString.Contains("mo vacilaum")) {
				await userMessage.Channel.SendMessageAsync("''Hmmmm vacilão... Teu cu na minha mao.''");
				return;
			}
			if (messageString.Contains("teu cu na minha mao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu e o aeroporto meu pau e o avião.''");
				return;
			}
			if (messageString.Contains("teu cu e o aeroporto meu pau e o aviao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a garagem meu pau é o caminhão.''");
				return;
			}
			if (messageString.Contains("teu cu e a garagem meu pau e o caminhao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a Carminha meu pau é o Tufão (ãnh?).''");
				return;
			}
			if (HasAllWords(messageString, new[] {"teu cu", "meu pau", "tufao"})) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o mar meu pau é o tubarão.''");
				return;
			}
			if (messageString.Contains("teu cu e o mar meu pau e o tubarao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o morro meu pau é o Complexo do Alemão.''");
				return;
			}
			if (messageString.Contains("teu cu e o morro meu pau e o complexo do alemao")) {
				await userMessage.Channel.SendMessageAsync("''Caraaalho, sem nexo.''");
				return;
			}
			if (messageString.Contains("sem nexo")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o cabelo meu pau é o reflexo.''");
				return;
			}
			if (HasAllWords(messageString, new[] {"teu cu e o cabelo", "meu pau e reflexo"})) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o Moon Walker meu pau é o Michael Jackson.''");
				return;
			}
			if (HasAllWords(messageString, new[] {"teu cu e o", "meu pau e o"})
				&& (HasAtLeastOneWord(messageString, new[] {"michael", "mickael", "maicow", " maycow", " maico", "jackson", "jackso", "jakso", "jakson", "jequiso", "jequison"})
					|| HasAtLeastOneWord(messageString, new[] {" moon ", " mun ", "walker", "walk", " uauquer"}))) {
				await userMessage.Channel.SendMessageAsync("''Ãhhnnnn Michael Jackson já morreu...''");
				return;
			}
			if (messageString.Contains("ja morreu") && HasAtLeastOneWord(messageString, new[] {"michael", "maicow", " maycow", " maico", "jackson", "jackso", "jakso", "jakson", "jequiso", "jequison"})) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a Julieta meu pau é o Romeu.''");
				return;
			}
			if (messageString.Contains("tu cu e a julieta") && messageString.Contains("meu pau e o romeu")) {
				await userMessage.Channel.SendMessageAsync("''Caraaalho, nada a vê.''");
				return;
			}
			if (messageString.StartsWith("nada a ve") || messageString == ("nada ve")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu pisca meu pau acende.''");
				return;
			}
			if (messageString.Contains("teu cu pisca") && messageString.Contains("meu pau acende")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a Globo meu pau é o SBT.''");
				return;
			}
			if (messageString.Contains("teu cu e a globo") && messageString.Contains("meu pau e o sbt")) {
				await userMessage.Channel.SendMessageAsync("''Aahhh vai toma no cu.''");
				return;
			}
			if (messageString.Contains("toma no cu")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o Pokemon meu pau é o Pikachu.''");
				return;
			}
			#endregion
			#endregion

			#region General
			if (messageString == "alguem ai") {
				await userMessage.Channel.SendMessageAsync("Eu. Mas sou um bot então não vou conseguir ter respostas para todas as suas perguntas.");
				return;
			}

			if (messageString.Contains("que horas sao")) {
				if (isQuestion) {
					await userMessage.Channel.SendMessageAsync("É hora de acertar as contas...");
					return;
				}
			}
			#endregion

			#region Insults
			// Answer to insults 

			if (messageString.Contains("bot lixo")
				|| messageString.Contains("suamaeeminha")
			) {
				await userMessage.AddReactionAsync(new Emoji(":eyes:"));
				await userMessage.Channel.SendMessageAsync("Algum problema " + rawMessage.Author.Mention + "?");
				return;
			}
			#endregion

			#region Links
			#region Black Yeast
			// Firsts
			if (HasAllWords(messageString, new[] {"black", "yeast"})) {
				// user is speaking about Black Yeast.
				await userMessage.Channel.SendMessageAsync(rawMessage.Author.Mention + " foi pausado por tempo indeterminado. Veja mais detalhes no site: https://chrisdbhr.github.io/blackyeast");
				return;
			}
			#endregion

			#region Canal
			if (HasAllWords(messageString, new[] {"canal", "youtube", "chris"})) {
				await userMessage.Channel.SendMessageAsync("Se quer saber qual o canal do Chris o link é esse: https://www.youtube.com/christopher7");
				return;
			}

			if (messageString.Contains("chris") && HasAtLeastOneWord(messageString, new[] {"face", "facebook"})) {
				await userMessage.Channel.SendMessageAsync("O link para o Facebook do Chris é esse: https://www.facebook.com/chrisdbhr");
				return;
			}

			if (messageString.Contains("twitch") && HasAtLeastOneWord(messageString, new[] {"seu", "canal"})) {
				await userMessage.Channel.SendMessageAsync("O link para o Twitch do Chris é esse: https://www.twitch.tv/chrisdbhr");
				return;
			}
			#endregion
			#endregion

			#region Public Commands
			if (messageString.EndsWith("comandos desconhecidos")) {
				if (userSaidHerName) {
					string readFile = File.ReadAllText("unknownCommands.txt");
					if (readFile != null && readFile.Length > 0) {
						string trimmedMsg = "Quando alguém fala algo que eu não conheço eu guardo em uma lista para o Chris ver depois. Essa é a lista de comandos que podem vir a receber respostas futuramente: " + Environment.NewLine + "`" + readFile + "`";
						await userMessage.Channel.SendMessageAsync(trimmedMsg.Substring(0, 1999));
						return;
					}
				}
			}

			// Best animes list
			if (userSaidHerName) {
				if (messageString == ("add a lista de melhores animes")) {
					messageString = messageString.Replace("add a lista de melhores animes", "");
					string filePath = "Lists/bestAnimes.txt";
					messageString.Trim();
					string file = File.ReadAllText(filePath);

					// first, compare if the text to save its not to big
					if (messageString.Length > 48) {
						// ignore the message because it can be spam
						return;
					}

					// check if the txt its not biggen then 10mb
					FileInfo fileInfo = new FileInfo(file);
					if (fileInfo.Length > 10 * 1000000) {
						await userMessage.Channel.SendMessageAsync("<@203373041063821313> eu tentei adicionar o texto que o " + userMessage.Author.Mention + " digitou mas o arquivo de lista de melhores animes alcançou o tamanho limite. :sob:");
						return;
					}

					// see if the anime is already on the list
					if (file.Contains(messageString)) {
						await userMessage.Channel.SendMessageAsync("O anime " + @"`{messageString}` ja esta na lista de melhores animes.");
						return;
					}
					else {
						File.AppendAllText(filePath, Environment.NewLine + messageString);
						await userMessage.Channel.SendMessageAsync("Adicionado " + @"`{messageString}` a lista de melhores animes. :wink:");
						return;
					}
				}
			}
			#endregion

			#region Lists
			// Voice commands list
			if (messageString == "lista de sons" || messageString == "list" || messageString == "lista" || messageString == ",, help" || messageString == ",,help" || messageString == ",help") {
				int stringMaxLength = 1999;
				StringBuilder answerText = new StringBuilder(stringMaxLength);
				answerText.AppendLine("**Posso tocar todos esses sons, toque eles com o comando ',, nomeDoSom':**");
				answerText.AppendLine("```");
				foreach (string s in Directory.GetFiles("Voices/").Select(Path.GetFileNameWithoutExtension)) {
					if (s.Length >= stringMaxLength) break;
					answerText.Append(s);
					answerText.Append(" | ");
				}
				answerText.AppendLine("```");
				await userMessage.Channel.SendMessageAsync(answerText.ToString().Substring(0, stringMaxLength - 3) + "...");
				return;
			}

			// Best Animes List
			if (userSaidHerName) {
				if (messageString == "best animes" || messageString == "melhores animes" || messageString == "lista de melhores animes" || messageString == "lista de animes bons" || messageString == "lista dos melhores animes") {
					string filePath = "Lists/bestAnimes.txt";
					string file = File.ReadAllText(filePath);
					if (!string.IsNullOrEmpty(file)) {
						// return the list
						await userMessage.Channel.SendMessageAsync("Lista de melhores animes:" + $"{file}");
					}
					else {
						// Create file if not exists
						File.WriteAllText(filePath, "");
					}
					return;
				}
			}

			//!!! THIS PART OF THE CODE BELOW MUST BE AS THE LAST BECAUSE:
			// see if user sayd only bot name on message with some other things and she has no answer yet
			if (userSaidHerName) {
				string unknownCommandsFileName = "Lists/unknownCommands.txt";
				string textToWrite = messageString + $"	({userMessage.Author.Username})";

				// first, compare if the text to save its not to big
				if (textToWrite.Length > 48) {
					// ignore the message because it can be spam
					return;
				}

				// check if the txt its not biggen then 10mb
				FileInfo fileInfo = new FileInfo(unknownCommandsFileName);
				if (fileInfo.Length > 10 * 1000000) {
					await userMessage.Channel.SendMessageAsync("<@203373041063821313> eu tentei adicionar o texto que o " + userMessage.Author.Username + " digitou mas o arquivo de lista de comandos alcançou o tamanho limite. :sob:");
					return;
				}

				// get text in string
				string fileContent = File.ReadAllText(unknownCommandsFileName);
				if (fileContent != null) {
					// only write if the unknown text is NOT already on the file
					if (!fileContent.Contains(messageString)) {
						File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
						await userMessage.AddReactionAsync(new Emoji(":grey_question:"));
						return;
					}
				}
				else {
					File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
					await userMessage.AddReactionAsync(new Emoji(":grey_question:"));
					return;
				}

				// return "Ainda não tenho resposta para isso:\n" + "`" + messageString + "`";
				return;
			}
			#endregion

			// if arrived here, the message has no answer.
		}

		/// <summary>
		/// Check if a string contains all defined words.
		/// </summary>
		/// <param name="message">Full string to compare.</param>
		/// <param name="s">Words to check.</param>
		/// <returns>Return if there is all of words in message.</returns>
		public static bool HasAllWords(string message, string[] s) {
			for (int i = 0; i < s.Length; i++) {
				if (!message.Contains(s[i])) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Check if a string contains at least one of defined words.
		/// </summary>
		/// <param name="message">Full string to compare.</param>
		/// <param name="s">Words to check.</param>
		/// <returns>Return true if there is a word in message.</returns>
		public static bool HasAtLeastOneWord(string message, string[] s) {
			return s.Any(c => message.Contains(c));
		}

		/// <summary>
		/// Chosse a string between an array of strings.
		/// </summary>
		/// <param name="s">strings to choose, pass as new[] { "option1", "option2", "..." }</param>
		/// <returns>return the choose string</returns>
		public static string ChooseAnAnswer(string[] s) {
			if (s.Length > 1) {
				return s[new System.Random().Next(0, s.Length)];
			}

			// equals one
			return s[0];
		}

		/// <summary>
		/// Remove special characters from string.
		/// </summary>
		/// <param name="text"></param>
		/// <returns>Return normalized string.</returns>
		public static string RemoveDiacritics(string text) {
			var normalizedString = text.Normalize(NormalizationForm.FormD);
			var stringBuilder = new StringBuilder();

			foreach (var c in normalizedString) {
				var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark) {
					stringBuilder.Append(c);
				}
			}

			return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
		}

		/// <summary>
		/// Removes bot string from message, and trim string, also set a boolean.
		/// </summary>
		public static string RemoveBotNameFromMessage(string messageString) {
			messageString = messageString.Replace("nyu", "");
			messageString = messageString.Replace("nuy", "");
			messageString = messageString.Trim();
			return messageString;
		}

		/// <summary>
		/// Create a new random object and return it.
		/// </summary>
		public static Random Randomize() {
			return new Random();
		}
	}
}
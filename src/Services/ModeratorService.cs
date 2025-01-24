using System.Linq;
using System.Threading.Tasks;
using App.Extensions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace App {
	public class ModeratorService(LoggingService _log)
	{
		readonly Dictionary<ulong, DateTime> _lastChangedChannelsTimes = new();
		readonly TimeSpan _cooldownToChangeTeamName = TimeSpan.FromMinutes(5);

		public async Task DeleteLastMessages(SocketInteractionContext context, int limit) {
			if (limit < 1 || limit > 500) {
				await context.Channel.SendMessageAsync("Invalid range");
				return;
			}

			var embed = new EmbedBuilder {
				Title = "Getting messages",
				Description = ""
			};

			var feedbackMsg = await context.Channel.SendMessageAsync("", false, embed.Build());

			_log.Debug("Getting all messages in channel");
			var lastMsgs = (await context.Channel.GetMessagesAsync(limit).FlattenAsync()).ToArray();

			int messagesCount = lastMsgs.Length;
			
			embed.Title = $"Cleaning {messagesCount} messages...";
			embed.Description = $"This can take some minutes";
			embed.Color = Color.Orange;
			await feedbackMsg.ModifyAsync(properties => properties.Embed = new Optional<Embed>(embed.Build()));
			
			_log.Info($"Starting deletion of {messagesCount} messages.");
			foreach (var msg in lastMsgs) {
				if (msg.Id == feedbackMsg.Id) continue;
				await msg.DeleteAsync();
			}
			
			embed.Title = $"Cleaned {messagesCount} messages";
			embed.Color = Color.Green;
			_log.Debug(embed.Title);
			embed.Description = "";
			await feedbackMsg.ModifyAsync(properties => properties.Embed = new Optional<Embed>(embed.Build()));

		}

		public async Task RenameTeam(string name, SocketGuild contextGuild,ISocketMessageChannel contextChannel)
		{
            if (!(contextChannel is SocketTextChannel textChannel)) return;
            if(string.IsNullOrWhiteSpace(name)) name = "equipe";

            var embed = new EmbedBuilder();
            IUserMessage msg = null;

            if (_lastChangedChannelsTimes.ContainsKey(contextChannel.Id)) {
                var nextTime = _lastChangedChannelsTimes[contextChannel.Id] + _cooldownToChangeTeamName;

                var now = DateTime.UtcNow;
                var waitTime = Math.Max((now - nextTime).TotalSeconds, 3);

                if (now <= nextTime) {
                    embed.Title = "calma";
                    embed.Description = $"a equipe mudou o nome agora a pouco, espera mais uns {waitTime:0} segundos pra tentar denovo";
                    embed.Color = Color.Orange;
                    await contextChannel.SendMessageAsync(string.Empty, false, embed.Build());
                    return;
                }
                else {
                    _lastChangedChannelsTimes.Remove(contextChannel.Id);
                }
            }

            try {
                // name
                var names = contextChannel.Name.Split('-').ToList();
                if (names.Count == 1) {
                    names.Add("");
                }

                if (names.Count < 2 && !(int.TryParse(names[0], out var teamNumber))) {
                    throw new Exception("names count is lesser than 2 or first item is not a number");
                }
                names[1] = string.Join('-', name);

                var fullName = $"{names[0]}-{names[1]}";

                // answer
                embed.Title = "perai q eu so lenta";
                embed.Description = $"vo tentar mudar o nome da equipe pra '{fullName}'";
                embed.Color = Color.Blue;

                msg = await contextChannel.SendMessageAsync(string.Empty, false, embed.Build());

                await textChannel.ModifyAsync(p => p.Name = fullName);
                await textChannel.Category.ModifyAsync(p => p.Name = fullName);

                foreach (var voiceChannel in contextGuild.VoiceChannels) {
                    if (voiceChannel.Category != textChannel.Category) continue;
                    await voiceChannel.ModifyAsync(p => p.Name = fullName);
                    break;
                }

                // done
                embed.Title = "Pronto";
                embed.Description = $"troquei o nome da equipe pra **{fullName}**, {GetNameChangeAnswer(names[1])}";
                embed.Color = Color.Green;
                _lastChangedChannelsTimes[contextChannel.Id] = DateTime.UtcNow;
                await msg.ModifyAsync(m => m.Embed = embed.Build());

            } catch (Exception e) {
                embed.Title = "oh no";
                embed.Description = $"{contextGuild.Owner.Mention} deu Exception em produção";
                embed.Footer = new EmbedFooterBuilder {
                    Text = e.Message.SubstringSafe(256)
                };
                embed.Color = Color.Red;

                _log.Error(e.ToString());

                if (msg != null) {
                    await msg.ModifyAsync(m => m.Embed = embed.Build());
                }
                else {
                    await contextChannel.SendMessageAsync("", false, embed.Build());
                }

            }
		}

		string GetNameChangeAnswer(string teamName) {
			teamName = ChatService.RemoveDiacritics(teamName);
			teamName = teamName.ToLower()
				.Replace(" ", string.Empty);

			if (teamName == "equipe") {
				return "nome padrão";
			}

			if (teamName == "rocket" || teamName == "equiperocket") {
				return "decolando pra lua";
			}

			if (teamName.Contains("naro") || teamName.Contains("taok") || teamName.Contains("bozo")) {
				return "ta ok";
			}

			if (teamName.Contains("studio")) {
				return "um studio melhor que CD Project Red";
			}

			if (teamName.Contains("team")) {
				return "team é equipe em ingles";
			}

			if (teamName.Contains("ufpr")) {
				return "e me deu saudade do RU";
			}

			if (teamName == "nomeaqui") {
				return "mas era pra vc digitar o nome da equipe no lugar de NOME AQUI";
			}


			var listOfDefaultAnswers = new[] {
				"mas nao gostei do nome",
				"mas achei q iam colocar outro nome",
				"mas eu queria outro nome",
				"um belo nome",
				"só gente bonita nessa equipe",
				"agora vao dormi",
				"agora vai beber agua",
				"mas continuo com fome",
				"igual o daquela outra equipe"
			};

			return listOfDefaultAnswers.RandomElement();
		}

	}
}

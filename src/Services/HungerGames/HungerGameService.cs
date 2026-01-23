using System.Reflection;
using App.Attributes;
using App.Extensions;
using App.HungerGames;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace App.Services.HungerGames;

[Service(ServiceLifetime.Singleton)]
public class HungerGameService : IDisposable {

	#region <<---------- Properties ---------->>

	readonly DiscordSocketClient _discord;
	readonly TimeSpan _timeToWaitEachMessage = TimeSpan.FromSeconds(5);

	public List<ulong> PlayingChannels = new List<ulong>();

	#endregion <<---------- Properties ---------->>




	#region <<---------- Initializers ---------->>

	public HungerGameService(DiscordSocketClient discord) {
		_discord = discord;
	}

	#endregion <<---------- Initializers ---------->>




	#region <<---------- JSON Keys ---------->>

	const string JKEY_CHANNEL_MATCHS_INFO_PREFIX = "Games/HungerGames/";

	#endregion <<---------- JSON Keys ---------->>




	#region <<---------- Enums ---------->>

	enum GameEndReason {
		victory, allDied, timeOut
	}

	#endregion <<---------- Enums ---------->>




	#region <<---------- General ---------->>

	public async Task NewHungerGameSimulation(IMessageChannel channel, IReadOnlyCollection<IUser> users, int numberOfPlayers) {
		var characters = new List<Character>();
		foreach (var user in users) {
			if (!(user is SocketGuildUser guildUser)) continue;
			if (guildUser.IsBot || guildUser.Status != UserStatus.Online) continue;
			characters.Add(new Character {
				User = guildUser
			});
		}

		if (characters.Count <= 1) return;

		characters = characters.Take(numberOfPlayers).ToList();

		// new match
		var embed = new EmbedBuilder {
			Color = Color.Green,
			Title = $"Nova partida de Hunger Games (Battle Royale)",
			Description = "Considerando apenas usuários ONLINE"
		};
		embed.AddField("Vivos", characters.Count(x => !x.IsDead), true);
		embed.AddField("Participantes", characters.Count, true);

		// footer version
		var version = Assembly.GetExecutingAssembly().GetName().Version;
		if (version == null) version = new Version(1, 0, 0);
		var build = version.Build;
		embed.WithFooter($"Hunger Games & Battle Royale Simulation - © CHRISdbhr", "https://chrisdbhr.github.io/images/avatar.png");

		await channel.SendMessageAsync(string.Empty, false, embed.Build());

		// game task
		await ProcessTurn(channel, characters);
	}

	async Task ProcessTurn(IMessageChannel channel, IReadOnlyCollection<Character> allCharacters) {

		// settings
		int maxTurns = 100;

		// game loop
		int currentTurn = 1;
		while (currentTurn < maxTurns) {
			EmbedBuilder embed;

			// is game canceled?
			if (!PlayingChannels.Contains(channel.Id)) {
				embed = new EmbedBuilder {
					Color = Color.Orange,
					Title = $"Jogo cancelado"
				};

				await Task.Delay(_timeToWaitEachMessage);
				await channel.SendMessageAsync(string.Empty, false, embed.Build());
				return;
			}


			// new turn
			embed = new EmbedBuilder {
				Color = Color.Default,
				Title = $"Rodada #{currentTurn}"
			};
			embed.AddField("Vivos", allCharacters.Count(x => !x.IsDead), true);
			embed.AddField("Participantes", allCharacters.Count, true);

			var mostKill = allCharacters.Aggregate((i1,i2) => i1.Kills > i2.Kills ? i1 : i2);
			if (mostKill != null && mostKill.Kills > 1){
				embed.AddField(mostKill.User.GetNameSafe(), $"matou mais, com {mostKill.Kills} mortes");
			}

			await Task.Delay(_timeToWaitEachMessage);
			await channel.SendMessageAsync(string.Empty, false, embed.Build());


			// foreach character
			foreach (var currentChar in allCharacters) {
				if (currentChar == null || currentChar.IsDead) continue;

				// get all alive
				var alive = allCharacters.Where(x => x != null && !x.IsDead).ToArray();

				// check for finish conditions
				if (alive.Length == 1) {
					// winner
					await EndGame(channel, GameEndReason.victory, allCharacters, alive[0]);
					return;
				}

				// everyone died
				if(alive.Length <= 0) {
					await EndGame(channel, GameEndReason.allDied, allCharacters);
					return;
				}

				await Task.Delay(_timeToWaitEachMessage);
				await currentChar.Act(channel, allCharacters);
			}
			currentTurn += 1;
		}


		// max turns reached, end game
		await EndGame(channel, GameEndReason.timeOut, allCharacters);
	}

	async Task EndGame(IMessageChannel channel, GameEndReason reason, IReadOnlyCollection<Character> allCharacters, Character winner = null) {
		await Task.Delay(_timeToWaitEachMessage);

		var embed = new EmbedBuilder {
			Title = "Fim de Jogo!"
		};


		switch (reason) {
			case GameEndReason.victory:
				embed.Color = Color.Green;

				// winner info
				if (winner == null) break;

				embed.WithThumbnailUrl(winner.User.GetAvatarUrl());

				embed.AddField("Vencedor", winner.User.Mention, true);

				// kills
				if (winner.Kills > 0) {
					embed.AddField("Mortes", $"Matou {winner.Kills} no total", true);
				}
				else {
					embed.AddField("Mortes", $"Ganhou sem matar ninguém!", true);
				}

				break;
			case GameEndReason.timeOut:
				embed.Color = Color.DarkOrange;
				embed.AddField("Catástrofe!", "Uma bomba atômica caiu e matou a todos!");
				break;
			case GameEndReason.allDied:
			default: // all died
				embed.Color = Color.DarkOrange;
				embed.AddField("Todos morreram", "Ninguém ganhou");
				break;
		}

		// more kills
		var mostKill = allCharacters.Aggregate((i1,i2) => i1.Kills > i2.Kills ? i1 : i2);
		if (mostKill != null && mostKill.Kills > 1) {
			var name = mostKill.User.GetNameBoldSafe();
			embed.AddField("Matador", $"{name} matou mais nessa partida, com {mostKill.Kills} mortes");
		}

		await channel.SendMessageAsync(string.Empty, false, embed.Build());
	}

	#endregion <<---------- General ---------->>




	#region <<---------- Diposables ---------->>

	public void Dispose() {

	}

	#endregion <<---------- Diposables ---------->>

}

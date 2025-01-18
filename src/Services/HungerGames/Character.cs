using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using App.Extensions;

namespace App.HungerGames {
	public class Character {
		
		public const int MAX_HUNGRY_LEVEL = 8;
		
		public IGuildUser User;
		public bool IsDead;
		public TurnAction LastAction;
		public int Kills;
		public Weapon CurrentWeapon;
		public int HungryLevel;

		
		
		
		public async Task Act(SocketCommandContext context, IReadOnlyCollection<Character> characters) {
			var embed = new EmbedBuilder();
			TurnAction chosenAction;
			var rand = new Random();
			float randValue = rand.Next(100);
			var charName = $"**{User.GetNameSafe()}**";

			// hungry
			if (HungryLevel >= MAX_HUNGRY_LEVEL) {
				IsDead = true;
				embed = new EmbedBuilder {
					Color = Color.Red,
					Title = $"{charName} morreu de fome!"
				};
				int kills = Kills;
				if (kills > 0) {
					embed.AddField("Contagem de mortes", kills, true);
				}
				await context.Channel.SendMessageAsync(string.Empty, false, embed?.Build());
				return;
			}
			
			embed.ThumbnailUrl = User.GetAvatarUrlSafe();

			// chose action
			var allPossibleActions = Enum.GetValues(typeof(TurnAction)).Cast<TurnAction>().ToList();
			do {
				chosenAction = allPossibleActions.RandomElement();
			} while (
				chosenAction == LastAction || 
				chosenAction == TurnAction.grabWeapon && CurrentWeapon != Weapon.none ||
				chosenAction == TurnAction.lookForFood && HungryLevel <= (MAX_HUNGRY_LEVEL * 0.5f)
			);
			
			var alive = characters.Where(x => !x.IsDead);

			if (embed.Footer == null && CurrentWeapon != Weapon.none) embed.WithFooter($"{User.GetNameSafe()} tem uma arma");
			
			// Choose Action
			switch (chosenAction) {
				case TurnAction.notSpecial:
					embed.Title = $"{charName} escolheu esperar...";
					int randInt = rand.Next(4);
					if (randInt == 1) {
						// run
						embed.Title = $"{charName} esta correndo!";
					}else if (randInt == 2) {
						// hide
						embed.Title = $"{charName} se escondeu...";
						
					}else if (randInt == 3) {
						// look to others
						var charToLook = alive.RandomElement();
						if (randValue > 50 && charToLook != this) {
							embed.Title = $"{charName} esta observando {charToLook.User.GetNameBoldSafe()}...";
						}
						else {
							embed.Title = $"{charName} esta procurando por outros jogadores...";
						}
					}
					
					break;
				case TurnAction.lookForFood:
					if (randValue > 70) {
						HungryLevel = 0;
						embed.Title = $"{charName} achou comida!";
					}
					else {
						embed.Title = $"{charName} esta com fome...";
						embed.AddField("Rodadas sem comer", $"{HungryLevel}/{MAX_HUNGRY_LEVEL}");
					}
					break;
				case TurnAction.grabWeapon:
					if (randValue > 70) {
						// found
						var allWeapons = Enum.GetValues(typeof(Weapon));
						var foundWeapon = (Weapon)allWeapons.GetValue(rand.Next(allWeapons.Length));

						embed.Title = $"{charName} achou uma arma!";
						
						CurrentWeapon = foundWeapon;
					}
					else {
						// not found
						embed.Title = $"{charName} procurou por uma arma mas não encontrou.";
					}
					break;
				case TurnAction.kill:
					var toKill = alive.RandomElement();
					
					embed = new EmbedBuilder {
						Color = Color.Red
					};
					
					// suicide ?
					if (toKill == this) {
						Kills += 1;
						IsDead = true;
						embed.WithThumbnailUrl(User.GetAvatarUrlSafe());
						embed.Title = $"{charName} se matou!";
						break;
					}
					
					var killer = this;
					var died = toKill;
					
					if (CurrentWeapon < toKill.CurrentWeapon) {
						killer = toKill;
						died = this;
					}
					killer.Kills += 1;
					died.IsDead = true;
					
					embed.WithThumbnailUrl(died.User.GetAvatarUrlSafe());
					string killCount = killer.Kills > 1 ? killer.Kills.ToString() : "";
					embed.WithFooter($"{killer.User.GetNameSafe()} matou {killCount}", killer.User.GetAvatarUrlSafe());
						
					embed.Title = $"{killer.User.GetNameBoldSafe()} matou {died.User.GetNameBoldSafe()}.";

					if(killer.Kills > 1) embed.AddField("Contagem de mortes", killer.Kills);
					break;
			}

			LastAction = chosenAction;
			HungryLevel += 1;

			await context.Channel.SendMessageAsync(string.Empty, false, embed?.Build());
		}
		
	}
}

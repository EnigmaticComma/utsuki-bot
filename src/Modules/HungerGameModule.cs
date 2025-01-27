using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.HungerGames;
using App.Services.HungerGames;
using Discord.Commands;

namespace App.Modules {
	[Name("Hunger Games")]
	public class HungerGameModule : ModuleBase<SocketCommandContext> {
		readonly HungerGameService _service;

		
		public HungerGameModule(HungerGameService service) {
			_service = service;
		}

		[Command("newhungergame"), Alias("nhg")]
		[Summary("Start a new Hunger Game simulation")]
		public async Task NewHungerGameSimulation() {
			if (Context?.Channel == null) return;
			var channelId = Context.Channel.Id;
			if (Context.Guild.Id == 798667749081481226 && channelId != 802832949460336660) return;
			
			await StopHungerGameSimulation();
			if (_service.PlayingChannels.Contains(Context.Channel.Id)) return; // already playing

			var usersAsyncEnum = Context.Channel.GetUsersAsync()?.GetAsyncEnumerator();
			if (usersAsyncEnum == null) return;
			var moved = await usersAsyncEnum.MoveNextAsync();
			if (!moved) return;

			var usersList = usersAsyncEnum.Current;
			if (!usersList.Any()) return;

			int numberOfPlayers = 500;

			_service.PlayingChannels.Add(channelId);
			await _service.NewHungerGameSimulation(Context, usersList, numberOfPlayers);
			_service.PlayingChannels.Remove(channelId);
		}
		
		[Command("stophungergame"), Alias("shg")]
		[Summary("Stop the Hunger Game simulation running in this channel")]
		public async Task StopHungerGameSimulation() {
			if (Context?.Channel == null) return;
			var channelId = Context.Channel.Id;
			if (!_service.PlayingChannels.Remove(channelId)) return;
			await ReplyAsync($"*Game will be canceled...*");
		}


		
	}
}

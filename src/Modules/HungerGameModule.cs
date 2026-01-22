using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.HungerGames;
using App.Services.HungerGames;
using Discord.Interactions;

namespace App.Modules {
	public class HungerGameModule : InteractionModuleBase<SocketInteractionContext> {
		readonly HungerGameService _service;

		public HungerGameModule(HungerGameService service) {
			_service = service;
		}

		[SlashCommand("newhungergame", "Start a new Hunger Game simulation")]
		public async Task NewHungerGameSimulation(int numberOfPlayers = 500) {
			if (Context?.Channel == null) return;
			var channelId = Context.Channel.Id;
			if (Context.Guild.Id == 798667749081481226 && channelId != 802832949460336660) {
                await RespondAsync("Este comando não pode ser usado neste canal.", ephemeral: true);
                return;
            }
			
			await StopHungerGameSimulationInternal();
			if (_service.PlayingChannels.Contains(Context.Channel.Id)) {
                await RespondAsync("Já existe uma simulação ocorrendo neste canal.", ephemeral: true);
                return;
            }

            await RespondAsync("Iniciando simulação do Hunger Games...");

			var usersAsyncEnum = Context.Channel.GetUsersAsync()?.GetAsyncEnumerator();
			if (usersAsyncEnum == null) return;
			var moved = await usersAsyncEnum.MoveNextAsync();
			if (!moved) return;

			var usersList = usersAsyncEnum.Current;
			if (!usersList.Any()) return;

			_service.PlayingChannels.Add(channelId);
			await _service.NewHungerGameSimulation(Context, usersList, numberOfPlayers);
			_service.PlayingChannels.Remove(channelId);
		}
		
		[SlashCommand("stophungergame", "Stop the Hunger Game simulation running in this channel")]
		public async Task StopHungerGameSimulation() {
			if (await StopHungerGameSimulationInternal()) {
                await RespondAsync("*Game will be canceled...*");
            } else {
                await RespondAsync("Não há simulação ocorrendo neste canal.", ephemeral: true);
            }
		}

        private async Task<bool> StopHungerGameSimulationInternal() {
            if (Context?.Channel == null) return false;
			var channelId = Context.Channel.Id;
			return _service.PlayingChannels.Remove(channelId);
        }
	}
}

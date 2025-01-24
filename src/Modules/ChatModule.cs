using Discord;
using Discord.Interactions;

namespace App.Modules {
	public class ChatModule : InteractionModuleBase<SocketInteractionContext> {
		const int DEFAULT_MESSAGES_AMOUNT = 500;
		readonly ChatService _service;



		
		public ChatModule(ChatService service) {
			_service = service;
		}
		
		
		
		
		[SlashCommand("randomphrase", "Get a random motivacional phrase")]
		public async Task GetRandomPhrase() {
			var phrase = await _service.GetRandomMotivationPhrase();
			if (string.IsNullOrEmpty(phrase)) return;
			var embed = new EmbedBuilder {
				Title = "Frase",
				Description = $"*\"{phrase}\"*",
				Footer = new EmbedFooterBuilder {
					Text = "pensador.com"
				}
			};
			
			await ReplyAsync(string.Empty, false, embed.Build());
		}

		[SlashCommand("remember","Get a random message from a user in current chat")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task RememberSomeMessage(int ammount) {
			await _service.GetAndRepplyRememberMessage(Context, ammount, true);
		}

		[SlashCommand("updatestatus", "Update bot self status")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task UpdateSelfStatus() {
			await _service.UpdateSelfStatusAsync();
		}

	}
}

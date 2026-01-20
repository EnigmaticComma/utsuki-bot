using System.Threading.Tasks;
using App.Attributes;
using App.Services;
using Discord;
using Discord.WebSocket;

namespace App {
	public class SleepService {

		public SleepService(DiscordSocketClient discord, LoggingService loggingService) {
			return;
			_discord = discord;
			_log = loggingService;
			_discord.MessageReceived += MessageReceived;
			_discord.UserUpdated += async (oldUser, newUser) => {
				if (!(newUser is SocketGuildUser socketGuildUser)) return;
				if (socketGuildUser.IsBot || socketGuildUser.IsWebhook) return;
				if (oldUser.Status == UserStatus.Offline && newUser.Status != UserStatus.Offline) {
					await InformUserWakeUp(socketGuildUser);
				}
			};
			_discord.UserVoiceStateUpdated += async (user, oldState, newState) => {
				if (!(user is SocketGuildUser socketGuildUser)) return;
				if (oldState.VoiceChannel == null && newState.VoiceChannel != null) {
					await InformUserWakeUp(socketGuildUser);
				}
			};
		}

		const string COLLECTION_AMIMIR = "amimir";
		readonly DiscordSocketClient _discord;

		readonly LoggingService _log;


		async Task MessageReceived(SocketMessage socketMessage) {
			if (!(socketMessage is SocketUserMessage userMessage)) return;
			if (userMessage.Source != MessageSource.User) return;

			// check user sleeping
			if (userMessage.Content?.ToLower().Replace(" ", string.Empty) == "amimir") {
				await SetUserIsSleeping(userMessage);
				return;
			}

			await InformUserWakeUp(userMessage.Author as SocketGuildUser);
		}

		async Task SetUserIsSleeping(SocketUserMessage msg) {
			await msg.AddReactionAsync(new Emoji("💤"));
		}

		async Task InformUserWakeUp(SocketGuildUser user) {
		}

		async Task PrivateMessageReceivedAsync(SocketUserMessage socketUserMessage, IDMChannel dmChannel) {
			
		}
		
	}
}

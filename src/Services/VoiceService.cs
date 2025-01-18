using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using App.Extensions;

namespace App {
	public class VoiceService {

		#region <<---------- Initializers ---------->>

		public VoiceService(DiscordSocketClient discord, LoggingService loggingService, GuildSettingsService guildSettings) {
			_disposable?.Dispose();
			_disposable = new CompositeDisposable();

			_guildSettings = guildSettings;
			_discord = discord;
			_log = loggingService;

			_discord.UserVoiceStateUpdated += OnUserVoiceChannelStateUpdate;
		}

		#endregion <<---------- Initializers ---------->>




		#region <<---------- Properties ---------->>

		CompositeDisposable _disposable;
		readonly DiscordSocketClient _discord;
		readonly LoggingService _log;
		readonly GuildSettingsService _guildSettings;

		DateTime _voiceChannelLastTimeRenamed;
		TimeSpan _voiceChannelIntervalToRename = TimeSpan.FromSeconds(15);
		
		#endregion <<---------- Properties ---------->>




		#region <<---------- Callbacks ---------->>

		async Task OnUserVoiceChannelStateUpdate(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState) {
			if (!(user is SocketGuildUser guildUser)) return;

			var sb = new StringBuilder();
			
			sb.Append($"[{guildUser.Guild?.Name}] ");
			sb.Append(guildUser.GetNameAndAliasSafe());

			// saiu do canal de voz
			if (oldState.VoiceChannel != null && newState.VoiceChannel == null) {
				sb.Append($" saiu do canal de voz '{oldState.VoiceChannel.Name}'");
			}

			// entrou no canal de voz
			else if (oldState.VoiceChannel == null && newState.VoiceChannel != null) {
				sb.Append($" entrou no canal de voz '{newState.VoiceChannel.Name}'");
			}
			
			else if (oldState.VoiceChannel != newState.VoiceChannel && oldState.VoiceChannel != null && newState.VoiceChannel != null) {
				sb.Append($" mudou do '{oldState.VoiceChannel.Name}' para '{newState.VoiceChannel.Name}'");
			}
			else {
				return;
			}

			await _log.Info(sb.ToString());
		}

		async Task OnUserUpdated(SocketUser oldUser, SocketUser newUser) {
			if (newUser is not SocketGuildUser user) return;

			var dynamicVoiceChannelsId = _guildSettings.GetGuildSettings(user.Guild.Id).DynamicVoiceChannels;
			if (dynamicVoiceChannelsId == null || dynamicVoiceChannelsId.Length <= 0) return;
			
			var allVoiceChannels = user.Guild.VoiceChannels;

			foreach (var voiceChannel in allVoiceChannels) {
				var usersOnVoiceChannel = user.Guild.Users.Where(x => x.VoiceChannel == voiceChannel).ToArray();
				if (usersOnVoiceChannel.Length <= 0) continue;
				
			}
			
			
		}

		#endregion <<---------- Callbacks ---------->>

	}
}

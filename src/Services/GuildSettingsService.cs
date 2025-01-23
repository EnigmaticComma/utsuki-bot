using System;
using System.Text;
using System.Threading.Tasks;
using App.Models;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace App {
	public class GuildSettingsService {
		
		public const string PATH_PREFIX = "GuildSettings/";

		readonly DiscordSocketClient _discord;
		readonly LoggingService _log;
		readonly Random _rand = new Random();

		public GuildSettingsService(DiscordSocketClient discord) { // cant access Log service here
			_discord = discord;

			_discord.UserJoined += async user => {
				await UserJoined(user);
			};
			
			_discord.UserLeft += UserLeft;
			_discord.UserBanned += UserBanned;
			
		}


		#region <<---------- Get Guild Settings ---------->>
		
		public GuildSettings GetGuildSettings(ulong guildId) {
			var path = $"{PATH_PREFIX}{guildId}";
			return JsonCache.LoadFromJson<GuildSettings>(path) ?? new GuildSettings();
		}
		
		#endregion <<---------- Get Guild Settings ---------->>

		async Task UserJoined(SocketGuildUser socketGuildUser) {
			_log.Info($"{socketGuildUser.Username} entrou no servidor {socketGuildUser.Guild.Name}");

			var guild = socketGuildUser.Guild;
			var guildSettings = GetGuildSettings(guild.Id);
			var channelId = guildSettings.JoinChannelId;
			if (channelId == null) return;
			
			var channel = guild.GetTextChannel(channelId.Value);
			if (channel == null) return;

			var msgText = socketGuildUser.IsBot ? "Ah não mais um bot aqui 😭" : $"Temos uma nova pessoinha no servidor, digam **oi** para {socketGuildUser.Mention}!";
			await channel.SendMessageAsync(msgText);
		}


		async Task UserBanned(SocketUser socketUser, SocketGuild socketGuild) {
			await UserLeavedGuild(socketUser, socketGuild, " saiu do servidor...");
		}

		async Task UserLeft(SocketGuild socketGuild, SocketUser socketUser) {
			await UserLeavedGuild(socketUser, socketGuild, " saiu do servidor.");
		}

		async Task UserLeavedGuild(SocketUser socketUser, SocketGuild socketGuild, string sufixMsg) {
			var guild = socketGuild;
			var guildSettings = GetGuildSettings(guild.Id);
			var channelId = guildSettings.JoinChannelId;
			if (channelId == null) return;
			
			var channel = guild.GetTextChannel(channelId.Value);
			if (channel == null) return;
			
			var jsonArray = JsonCache.LoadFromJson<JArray>("Answers/UserLeave");
			string customAnswer = null;
			if (jsonArray != null) {
				customAnswer = jsonArray[_rand.Next(0, jsonArray.Count)].Value<string>();
			}
			
			var embed = new EmbedBuilder {
				Description = $"Temos {socketGuild.MemberCount} membros agora.",
				Color = Color.Red
			};
			
			var title = new StringBuilder();
			title.Append($"{socketUser.Username}#{socketUser.DiscriminatorValue:0000}");
			title.Append($"{sufixMsg}");

			embed.Title = title.ToString();
			
			// just leaved guild
			if (socketUser is SocketGuildUser socketGuildUser) {
				title.Append($"{(socketGuildUser.Nickname != null ? $" ({socketGuildUser.Nickname})" : null)}");

				if (socketGuildUser.JoinedAt.HasValue) {
					embed.Footer = new EmbedFooterBuilder {
						Text = $"Membro desde {socketGuildUser.JoinedAt.Value.ToString("dd/MM/yy hh tt")}"
					};
				}
			}
			else {
				// was banned
				var guildOwner = socketGuild.Owner;
				await guildOwner.SendMessageAsync($"Banido do servidor {socketGuild.Name}", false, embed.Build());
			}
			
			var sendMsg = await channel.SendMessageAsync(socketUser.IsBot ? "Era um bot" : customAnswer, false, embed.Build());
			await sendMsg.AddReactionAsync(new Emoji(":regional_indicator_f:"));
		}


	}
}

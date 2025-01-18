using System.Threading.Tasks;
using App.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace App.Modules {
	public class GuildSettingsModule : ModuleBase<SocketCommandContext> {

		private GuildSettingsService _service;
		
		public GuildSettingsModule(GuildSettingsService service) {
			_service = service;
		}
	
		

		#region <<---------- User Leave and Join ---------->>
		
		[Command("setjoinchannel")]
		[Summary("Set channel to notify when user joins guild.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetJoinChannel(SocketTextChannel textChannel) {
			var path = $"{GuildSettingsService.PATH_PREFIX}{Context.Guild.Id}";
			var guildSettings = JsonCache.LoadFromJson<GuildSettings>(path) ?? new GuildSettings();

			guildSettings.JoinChannelId = textChannel?.Id;
			JsonCache.SaveToJson(path, guildSettings);

			var embed = new EmbedBuilder();
			if (textChannel != null) {
				embed.Title = $"Join channel set to";
				embed.Description = textChannel.Mention;
			}
			else {
				embed.Title = $"Disabled join guild messages";
			}

			await  ReplyAsync("", false, embed.Build());
		}
		
		[Command("setleavechannel")]
		[Summary("Set channel to notify when user leaves guild.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetLeaveChannel(SocketTextChannel textChannel) {
			var path = $"{GuildSettingsService.PATH_PREFIX}{Context.Guild.Id}";
			var guildSettings = JsonCache.LoadFromJson<GuildSettings>(path) ?? new GuildSettings();
			
			guildSettings.LeaveChannelId = textChannel?.Id;
			JsonCache.SaveToJson(path, guildSettings);

			var embed = new EmbedBuilder();
			if (textChannel != null) {
				embed.Title = $"Leave channel set to";
				embed.Description = textChannel.Mention;
			}
			else {
				embed.Title = $"Disabled Leave guild messages";
			}

			await  ReplyAsync("", false, embed.Build());
		}

		#endregion <<---------- User Leave and Join ---------->>

		
		
		
		
	}
}

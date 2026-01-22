using System.Threading.Tasks;
using App.Models;
using App.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace App.Modules {
	public class GuildSettingsModule(GuildSettingsService _service) : InteractionModuleBase<SocketInteractionContext> {

		#region <<---------- User Leave and Join ---------->>
		
		[SlashCommand("setjoinchannel", "Set channel to notify when user joins guild.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetJoinChannel(SocketTextChannel? textChannel = null) {
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

			await RespondAsync(embed: embed.Build());
		}
		
		[SlashCommand("setleavechannel", "Set channel to notify when user leaves guild.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetLeaveChannel(SocketTextChannel? textChannel = null) {
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

			await RespondAsync(embed: embed.Build());
		}

		#endregion <<---------- User Leave and Join ---------->>
		
		#region <<---------- Dynamic Voice ---------->>

		[SlashCommand("setvoicehub", "Set the voice channel that triggers dynamic channel creation.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetVoiceHub(SocketVoiceChannel? voiceChannel = null) {
			var path = $"{GuildSettingsService.PATH_PREFIX}{Context.Guild.Id}";
			var guildSettings = JsonCache.LoadFromJson<GuildSettings>(path) ?? new GuildSettings();

			guildSettings.DynamicVoiceSourceId = voiceChannel?.Id;
			JsonCache.SaveToJson(path, guildSettings);

			var embed = new EmbedBuilder();
			if (voiceChannel != null) {
				embed.Title = $"Dynamic Voice Hub set to";
				embed.Description = voiceChannel.Name;
			}
			else {
				embed.Title = $"Disabled Dynamic Voice Hub";
			}
			
			embed.Color = Color.Green;
			await RespondAsync(embed: embed.Build());
		}

		#endregion <<---------- Dynamic Voice ---------->>
	}
}

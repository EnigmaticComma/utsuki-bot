using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using App.Attributes;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using App.Extensions;
using App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace App {
	[Service(ServiceLifetime.Singleton)]
	public class BackupService {

		#region <<---------- Properties ---------->>

		readonly DiscordSocketClient _discord;
		readonly GuildSettingsService _guildSettings;
		readonly LoggingService _log;

		public const string TempDirPrefix = "temp/"; // keep the '/' at the end

		#endregion <<---------- Properties ---------->>


		

		#region <<---------- Initializers ---------->>
		
		public BackupService(DiscordSocketClient discord, LoggingService loggingService, GuildSettingsService guildSettings) {
			_discord = discord;
			_log = loggingService;
			_guildSettings = guildSettings;

			_discord.MessageReceived += MessageWithAttachmentReceived;
		}
		
		#endregion <<---------- Initializers ---------->>


		async Task MessageWithAttachmentReceived(SocketMessage socketMessage) {
			if (socketMessage.Author.IsBot || socketMessage.Author.IsWebhook) return;
			if (socketMessage.Channel is not SocketGuildChannel guildChannel) return;

			var guildId = guildChannel.Guild.Id;
			
			// check for attachment backup channel on guild
			var guildSettings = _guildSettings.GetGuildSettings(guildId);

			if (guildSettings == null || guildSettings.AttachmentsBackupChannelId == null) return;

			var guild = _discord.GetGuild(guildId);
			if (guild == null) return;
			var channel = guild.GetTextChannel(guildSettings.AttachmentsBackupChannelId.Value);
			if (channel == null) return;

			var attachmentsPaths = await BackupAttachments(socketMessage.Attachments);
			
			var embed = new EmbedBuilder {
				Title = $"From {socketMessage.Author.Username}",
				Fields = {
					new EmbedFieldBuilder {
						Name = "Author",
						Value = socketMessage.Author.Mention
					},
					new EmbedFieldBuilder {
						Name = "Channel",
						Value = socketMessage.Channel.Name
					}
				}
			};

			foreach (var path in attachmentsPaths) {
				var msg = await channel.SendFileAsync(path, socketMessage.Content, false, embed.Build());
				if (msg == null) {
					_log.Error($"Could not backup file '{path}' on text channel '{channel.Name}'.");
				}
				else {
					File.Delete(path);
				}
			}
		}

		/// <summary>
		/// Returns list of attachments backup file names and paths.
		/// </summary>
		public async Task<string[]> BackupAttachments(IReadOnlyCollection<Attachment> Attachments) {
			var filePathsAndNames = new List<string>();
			
			foreach (var attachment in Attachments) {
				var dateTime = DateTime.UtcNow;
				
				var targetDir = "temp/";
				Directory.CreateDirectory(targetDir);
				
				var fileName = $"{dateTime.Year}-{dateTime.Month:00}-{dateTime.Day:00}_{dateTime.Hour:00}-{dateTime.Minute:00}_{attachment.Filename}";
				
				var filePathAndName = Path.Combine(targetDir, fileName);
				if (File.Exists(filePathAndName)) {
					filePathAndName = $"{dateTime.Ticks}_{filePathAndName}";
				}
				using (var client = new WebClient()) {
					try { 				
						await client.DownloadFileTaskAsync(new Uri(attachment.Url), filePathAndName);
					} catch (Exception e) {
						Console.WriteLine(e);
					}
				}
				
				filePathsAndNames.Add(filePathAndName);

			}

			return filePathsAndNames.ToArray();
		}
		
	}
}

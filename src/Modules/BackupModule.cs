using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using App.Extensions;
using App.Services;

namespace App.Modules {
	public class BackupModule(BackupService _service) : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("backupchannel", "Backups a channel messages and media")]
        [RequireBotPermission(GuildPermission.ReadMessageHistory)]
        [RequireBotPermission(GuildPermission.ViewChannel)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task BackupChannel(int messagesLimit = 100) {
			if (messagesLimit <= 0) return;
			if (Context.Channel is not SocketTextChannel textChannel) {
                await RespondAsync("Este comando só pode ser usado em canais de texto.", ephemeral: true);
                return;
            }

			await RespondAsync("Starting backup\nReading messages...");
            var firstResponse = await GetOriginalResponseAsync();

			var allAttachments = new List<Attachment>();
			var sb = new StringBuilder();

			var messagesRaw = await textChannel.GetMessagesAsync(messagesLimit).FlattenAsync();
            var allMsgs = messagesRaw.ToList();

			foreach (var message in allMsgs) {
				if (message is not RestUserMessage userMessage || message.Author.IsBot) continue;

				// backup media
				if (userMessage.Attachments != null && userMessage.Attachments.Count > 0) allAttachments.AddRange(userMessage.Attachments);

				// backup text
				var msgResolved = userMessage.Resolve(
					TagHandling.FullNameNoPrefix,
					TagHandling.FullNameNoPrefix,
					TagHandling.FullName,
					TagHandling.FullName
				);

				if (string.IsNullOrEmpty(msgResolved)) continue;
				if (message.Author != null) {
					sb.Append(message.Author.Id.ToString());
					if (message.Author is RestGuildUser restGuildUser) {
						sb.Append('\t');
						sb.Append(restGuildUser.GetNameAndAliasSafe());
					}
					else {
						sb.Append('\t');
						sb.Append(message.Author.Username ?? "Deleted user");
					}
				}
				
				sb.Append('\t');
				sb.Append(msgResolved.Replace('\t',' ').Replace('\n', ' ').Replace('\r', ' ').Replace(Environment.NewLine, string.Empty));
				sb.AppendLine();
			}
				
			await ModifyOriginalResponseAsync(p => p.Content = $"{firstResponse.Content}\nSaving {allMsgs.Count} messages to disk...");

			var dir = Path.Combine("Backups", Context.Channel.Id.ToString());

			try {
				Directory.CreateDirectory(dir);
				File.WriteAllText(Path.Combine(dir, Context.Channel.Id.ToString() + ".tsv"), sb.ToString(), Encoding.UTF8);

			} catch (Exception e) {
				Console.WriteLine(e);
			}
			
			if (allAttachments.Count > 0) {
				await ModifyOriginalResponseAsync(p => p.Content = $"{firstResponse.Content}\nDownloading {allAttachments.Count} attachments...");

				var backupPaths = await _service.BackupAttachments(allAttachments);
				if (backupPaths.Length > 0) {
					await ModifyOriginalResponseAsync(p => p.Content = $"{firstResponse.Content}\nMoving {backupPaths.Length} files to folder...");

					foreach (var backupPath in backupPaths) {
						var nameSeparated = backupPath.Replace(BackupService.TempDirPrefix, string.Empty);
						var destinyFileName = Path.Combine(dir, nameSeparated);
						try {
							File.Move(backupPath, destinyFileName);
						} catch (Exception e) {
							Console.WriteLine(e);
						}
					}
				}
			}
			
			await ModifyOriginalResponseAsync(p=>p.Content = $"{firstResponse.Content}\nDone ✔");
		}
	}
}

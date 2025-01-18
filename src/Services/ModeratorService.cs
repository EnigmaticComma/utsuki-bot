using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace App {
	public class ModeratorService {
		readonly LoggingService _log;
		
		public ModeratorService(LoggingService loggingService) {
			_log = loggingService;
		}

		public async Task DeleteLastMessages(SocketCommandContext context, int limit) {
			if (limit < 1 || limit > 500) {
				await context.Message.AddReactionAsync(new Emoji("🚫"));
				return;
			}

			var embed = new EmbedBuilder {
				Title = "Getting messages",
				Description = ""
			};

			var feedbackMsg = await context.Channel.SendMessageAsync("", false, embed.Build());

			await _log.Debug("Getting all messages in channel");
			var lastMsgs = (await context.Channel.GetMessagesAsync(limit).FlattenAsync()).ToArray();

			int messagesCount = lastMsgs.Length;
			
			embed.Title = $"Cleaning {messagesCount} messages...";
			embed.Description = $"This can take some minutes";
			embed.Color = Color.Orange;
			await feedbackMsg.ModifyAsync(properties => properties.Embed = new Optional<Embed>(embed.Build()));
			
			await _log.Info($"Starting deletion of {messagesCount} messages.");
			foreach (var msg in lastMsgs) {
				if (msg.Id == feedbackMsg.Id) continue;
				await msg.DeleteAsync();
			}
			
			embed.Title = $"Cleaned {messagesCount} messages";
			embed.Color = Color.Green;
			await _log.Debug(embed.Title);
			embed.Description = "";
			await feedbackMsg.ModifyAsync(properties => properties.Embed = new Optional<Embed>(embed.Build()));

		}

	}
}

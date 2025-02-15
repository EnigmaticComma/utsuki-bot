using App.Attributes;
using Discord;
using Discord.Interactions;

namespace App.Services;

[Service]
public class ModeratorService(LoggingService _log)
{

	public async Task DeleteLastMessages(SocketInteractionContext context, int limit) {
		if (limit < 1 || limit > 500) {
			await context.Channel.SendMessageAsync("Invalid range");
			return;
		}

		var embed = new EmbedBuilder {
			Title = "Getting messages",
			Description = ""
		};

		var feedbackMsg = await context.Channel.SendMessageAsync("", false, embed.Build());

		_log.Debug("Getting all messages in channel");
		var lastMsgs = (await context.Channel.GetMessagesAsync(limit).FlattenAsync()).ToArray();

		int messagesCount = lastMsgs.Length;
			
		embed.Title = $"Cleaning {messagesCount} messages...";
		embed.Description = $"This can take some minutes";
		embed.Color = Color.Orange;
		await feedbackMsg.ModifyAsync(properties => properties.Embed = new Optional<Embed>(embed.Build()));
			
		_log.Info($"Starting deletion of {messagesCount} messages.");
		foreach (var msg in lastMsgs) {
			if (msg.Id == feedbackMsg.Id) continue;
			await msg.DeleteAsync();
		}
			
		embed.Title = $"Cleaned {messagesCount} messages";
		embed.Color = Color.Green;
		_log.Debug(embed.Title);
		embed.Description = "";
		await feedbackMsg.ModifyAsync(properties => properties.Embed = new Optional<Embed>(embed.Build()));

	}

}
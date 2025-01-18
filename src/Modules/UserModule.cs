using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using App.Extensions;

namespace App.Modules {
	public class UserModule : ModuleBase<SocketCommandContext> {

		[Command("profilepicture"), Alias("pp")]
		[Summary("Get user profile picture")]
		public async Task GetUserProfilePicture(SocketGuildUser user = null) {
			var userAvatarUrl = user.GetAvatarUrlSafe();
			
			var uri = new Uri(userAvatarUrl);
			var urlString = userAvatarUrl.Replace(uri.Query, string.Empty);
			
			var embed = new EmbedBuilder();
			embed.Title = $"Foto de perfil de {user.GetNameSafe()}";
			embed.ImageUrl = urlString;

			await ReplyAsync("", false, embed.Build());
			await Context.Message.DeleteAsync();
		}

		[Command("accounttime"), Alias("actime")]
		[Summary("Get other user account creation time")]
		public async Task GetUserAccountTime(SocketUser user = null) {
			user ??= Context.User;
			
			var now = DateTime.UtcNow;
			var created = user.CreatedAt.UtcDateTime;
			var difference = (now - created);
			
			var totalMonths = difference.TotalDays / 30.4;
			var years = totalMonths / 12;
			var months = totalMonths % 12;
			var days = difference.TotalDays / 30.4;
			
			var sb = new StringBuilder();
			if (years > 0) sb.Append($"{years:0} ano(s), ");
			if (months > 0) sb.Append($"{months:0} mes(es), ");
			if (days > 0) sb.Append($"{days:0} dias");
			
			var e = new EmbedBuilder {
				Title = sb.ToString(),
				Description = $"Tempo de conta de {user.Mention}"
			};
			e.ImageUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();

			await ReplyAsync(Context.User.Mention, false, e.Build());
		}

	}
}

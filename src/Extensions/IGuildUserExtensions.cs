using Discord;

namespace App.Extensions {
	public static class IGuildUserExtensions {
		public static string GetNameSafe(this IGuildUser guildUser) {
			return guildUser == null ? null : guildUser.Nickname ?? guildUser.Username;
		}
		
		public static string GetNameBoldSafe(this IGuildUser guildUser) {
			return guildUser == null ? null : $"**{guildUser.Nickname ?? guildUser.Username}**";
		}
		
		public static string GetNameAndAliasSafe(this IGuildUser guildUser) {
			return guildUser == null ? null : guildUser.Username + (string.IsNullOrEmpty(guildUser.Nickname) ? string.Empty : $"({guildUser.Nickname})");
		}

		public static string GetAvatarUrlSafe(this IGuildUser guildUser) {
			return guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl();
		}

		public static string GetNameOrNickSafe(this IUser user) {
			if (user is IGuildUser guildUser) {
				return string.IsNullOrEmpty(guildUser.Nickname) ? user.Username : guildUser.Nickname;
			}
			return user.Username;
		}

	}
}

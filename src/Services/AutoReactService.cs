using System.Threading.Tasks;
using Discord.WebSocket;

namespace App {
	public class AutoReactService {

		public AutoReactService(DiscordSocketClient discord) {
			_discord = discord;
			_discord.MessageReceived += OnMessageReceived;
		}


		readonly DiscordSocketClient _discord;


		async Task OnMessageReceived(SocketMessage message) {
			if (message.Attachments.Count <= 0) return;
			
		}
		
	}
}

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace App.Modules {
	public class LoggingModule : ModuleBase<SocketCommandContext> {

		private const int DEFAULT_BUFFER_SIZE = 4096;
		private readonly LoggingService _loggingService; 
		
		public LoggingModule(LoggingService loggingService) {
			_loggingService = loggingService;
		}

		#region <<---------- Commands ---------->>
		
		[Command("logs")]
		[Alias("log")]
		[Summary("Get today log file.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task GetLogFiles() {
			await using (var fileStream = new FileStream(_loggingService.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, DEFAULT_BUFFER_SIZE, (FileOptions.Asynchronous | FileOptions.SequentialScan))) {
				await Context.Channel.SendFileAsync(fileStream, _loggingService.LogFile.Split('/').Last());
			}
		}

		#endregion <<---------- Commands ---------->>
		
	}
}

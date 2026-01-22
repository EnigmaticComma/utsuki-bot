using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App.Services;
using Discord;
using Discord.Interactions;

namespace App.Modules {
	public class LoggingModule : InteractionModuleBase<SocketInteractionContext> {
		const int DEFAULT_BUFFER_SIZE = 4096;
		readonly LoggingService _loggingService; 
		
		public LoggingModule(LoggingService loggingService) {
			_loggingService = loggingService;
		}

		#region <<---------- Commands ---------->>
		
		[SlashCommand("logs", "Get today log file.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task GetLogFiles() {
            if (!File.Exists(_loggingService.LogFile)) {
                await RespondAsync("Nenhum arquivo de log encontrado para hoje.", ephemeral: true);
                return;
            }

            await DeferAsync();
			await using (var fileStream = new FileStream(_loggingService.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, DEFAULT_BUFFER_SIZE, (FileOptions.Asynchronous | FileOptions.SequentialScan))) {
				await Context.Channel.SendFileAsync(fileStream, _loggingService.LogFile.Split('/').Last());
			}
            await FollowupAsync("Log file sent.");
		}

		#endregion <<---------- Commands ---------->>
		
	}
}

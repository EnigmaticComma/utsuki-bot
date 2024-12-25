namespace UtsukiBot;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Services;

public class Startup : IDisposable {

        ServiceProvider _serviceProvider;

        public Startup(string[] args) { }

        public static async Task RunAsync(string[] args) {
            var startup = new Startup(args);
            await startup.RunAsync();
        }

        public async Task RunAsync() {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            _serviceProvider.GetRequiredService<DynamicVoiceChannelService>();

            await _serviceProvider.GetRequiredService<StartupService>().StartAsync();
            Console.WriteLine("Started");
            await Task.Delay(-1);                               // Keep the program alive
        }

        private void ConfigureServices(IServiceCollection services) {
            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {                                       // Add discord to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                MessageCacheSize = 1000             // Cache 1,000 messages per channel
            }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {                                       // Add the command service to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                DefaultRunMode = RunMode.Async,     // Force all commands to run async by default
            }))
            .AddSingleton<DynamicVoiceChannelService>()
            .AddSingleton<Random>()
            .AddSingleton<StartupService>();
        }

        public void Dispose() {
            this._serviceProvider.Dispose();
        }
    }
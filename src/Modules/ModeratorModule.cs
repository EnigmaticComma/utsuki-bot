using Discord;
using Discord.WebSocket;
using App.Extensions;
using App.Services;
using Discord.Interactions;
using RestSharp;

namespace App.Modules {

    public class ModeratorModule : InteractionModuleBase<SocketInteractionContext> {

        public InteractionService _commands { get; set; }
        readonly InteractionHandler _handler;
        readonly ModeratorService _moderatorService;
        readonly LoggingService _log;
        readonly DiscordSocketClient _discord;

        public ModeratorModule(DiscordSocketClient discord, ModeratorService moderatorService, LoggingService loggingService, InteractionHandler handler,InteractionService commands) {
            _moderatorService = moderatorService;
            _log = loggingService;
            _discord = discord;
            _handler = handler;
            _commands = commands;

            _discord.MessageReceived += async message => {
                if (message.Source != MessageSource.User) return;
                // mock ggj staff role
                if(message.MentionedUsers.Count <= 0 || !message.Content.Contains("staff",StringComparison.InvariantCultureIgnoreCase)) {
                    return;
                }
                if(message.Author is not SocketGuildUser sgd) {
                    return;
                }
                _log.Info($"Setting Staff role to '{message.Author.GetNameOrNickSafe()}'");
                await sgd.AddRoleAsync(1328551591250366571);
                await message.AddReactionAsync(new Emoji("👍"));
            };
        }

        [SlashCommand("renamevoicechannel", "Renames a voice channel that user is in." )]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task RenameVoiceChannel(string newName) {
            if (newName.Length <= 0) return;
            if (!(Context.User is SocketGuildUser user)) return;
            var vc = user.VoiceChannel;
            if (vc == null) {
                await RespondAsync("Você não está em um canal de voz.", ephemeral: true);
                return;
            }
            await vc.ModifyAsync(p => p.Name = newName);
            await RespondAsync($"Canal de voz renomeado para: {newName}");
        }

        [SlashCommand("randomimg", "Get random image from picsum")]
        public async Task GetRandomImg(int desiredResolution)
        {
            await DeferAsync();
            var client = new RestClient();
            var timeline = await client.ExecuteAsync(new RestRequest($"https://picsum.photos/{desiredResolution}", Method.Get));

            var embed = new EmbedBuilder {
                Title = "Random image",
                Description = "from picsum.photos",
                ThumbnailUrl = timeline.ResponseUri.OriginalString
            };

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("deletelastmessages", "Delete a number of messages in current channel")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task DeleteLastMessages(int limit) {
            await RespondAsync($"Deletando as últimas {limit} mensagens...", ephemeral: true);
            await _moderatorService.DeleteLastMessages(Context, limit);
        }


        [SlashCommand("getchannelinfo", "Get a channel name by id")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task GetTextChannelInfo(ulong channelId) {
            var channel = Context.Guild.GetChannel(channelId);
            if (channel == null) {
                await RespondAsync("nao achei canal com esse id", ephemeral: true);
                return;
            }

            await RespondAsync($"nome do canal: {channel.Name}");

        }

    }
}

using Discord;
using Discord.WebSocket;
using App.Extensions;
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
            if (vc == null) return;
            await vc.ModifyAsync(p => p.Name = newName);
        }

        [SlashCommand("randomimg", "Get random image from picsum")]
        public async Task GetRandomImg(int desiredResolution)
        {
            await RespondAsync("Getting random image");
            var client = new RestClient();
            var timeline = await client.ExecuteAsync(new RestRequest($"https://picsum.photos/{desiredResolution}", Method.Get));

            var embed = new EmbedBuilder {
                Title = "Random image",
                Description = "from picsum.photos",
                ThumbnailUrl = timeline.ResponseUri.OriginalString
            };

            await RespondAsync("",[embed.Build()]);
        }

        [SlashCommand("deletelastmessages", "Delete a number of messages in current channel")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task DeleteLastMessages(int limit) {
            await _moderatorService.DeleteLastMessages(Context, limit);
        }

        [SlashCommand("newteam", "Creates X new teams with text and voice channel")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task CreateTeam(int numberOfTeams)
        {
            await RespondAsync($"Starting to create {numberOfTeams} teams", null, false, true);
            var guild = Context.Guild;
            const string DefaultTeamName = "equipe";
            _log.Info($"Starting to create {numberOfTeams} teams");

            for (int i = 0; i < numberOfTeams; i++) {
                var channelName = $"{i:000}-{DefaultTeamName}";
                _log.Info($"Creating team {i+1}/{numberOfTeams}");
                var category = await guild.CreateCategoryChannelAsync(channelName );
                await category.ModifyAsync(
                    p => {
                        p.Position = (int) 999;
                    });

                var textChannel = await guild.CreateTextChannelAsync(channelName, p => { p.CategoryId = category.Id; });
                await textChannel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, new OverwritePermissions(
                    PermValue.Inherit,
                    PermValue.Inherit,
                    PermValue.Inherit,
                    PermValue.Inherit,
                    PermValue.Inherit,
                    PermValue.Inherit,
                    PermValue.Allow
                ));

                await guild.CreateVoiceChannelAsync(channelName, p => {
                    p.CategoryId = category.Id;
                    p.Bitrate = 32000;
                });
            }

        }

        // [SlashCommand("deleteallteams", "Destroy all team channels!")]
        // [RequireUserPermission(GuildPermission.Administrator)]
        // [RequireBotPermission(GuildPermission.ManageChannels)]
        // public async Task DeleteAllTeams() {
        //     var guild = Context.Guild;
        //
        //     var embed = new EmbedBuilder();
        //     IUserMessage msg = null;
        //
        //     try {
        //
        //         _log.Info($"Will delete all Teams in {guild.Name}");
        //
        //         var allTextChannels = guild.TextChannels.Where(s => int.TryParse(s.Name.Split('-')[0], out _));
        //         var allVoiceChannles = guild.VoiceChannels.Where(s => int.TryParse(s.Name.Split('-')[0], out _));
        //         var allCategories = guild.CategoryChannels.Where(s => int.TryParse(s.Name.Split('-')[0], out _));
        //
        //         _log.Info($"Texts {allTextChannels.Count()}, Voices {allVoiceChannles.Count()}, Cats {allCategories.Count()}");
        //
        //
        //         foreach (var c in allTextChannels) {
        //             await Task.Delay(1000);
        //             if (c == null) continue;
        //             _log.Info($"Deleting {c.Name}");
        //             await c.DeleteAsync();
        //         }
        //         foreach (var c in allVoiceChannles) {
        //             await Task.Delay(1000);
        //             if (c == null) continue;
        //             _log.Info($"Deleting {c.Name}");
        //             await c.DeleteAsync();
        //         }
        //         foreach (var c in allCategories) {
        //             await Task.Delay(1000);
        //             if (c == null) continue;
        //             _log.Info($"Deleting {c.Name}");
        //             await c.DeleteAsync();
        //         }
        //
        //     } catch (Exception e) {
        //         embed.Title = "oh no";
        //         embed.Description = $"{Context.Guild.Owner.Mention} socorro nao consegui destruir tudo";
        //         embed.Footer = new EmbedFooterBuilder {
        //             Text = e.Message.SubstringSafe(256)
        //         };
        //         embed.Color = Color.Red;
        //
        //         _log.Error(e.ToString());
        //
        //         if (msg != null) {
        //             await msg.ModifyAsync(m => m.Embed = embed.Build());
        //         }
        //         else {
        //             await ReplyAsync(string.Empty, false, embed.Build());
        //         }
        //
        //     }
        // }

        [SlashCommand("teamname", "Set the team name")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task RenameTeam(string name)
        {
            await RespondAsync($"Definindo nome da equipe para {name}");
            await _moderatorService.RenameTeam(name, Context.Guild, Context.Channel);
        }



        [SlashCommand("teampins", "set pinned message")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetPins() {
            int numberChanged = 0;
            foreach (var textChannel in Context.Guild.TextChannels) {
                var nameSplited = textChannel.Name.Split('-');
                _log.Info($"Vendo canal {textChannel.Name}");
                foreach (var name in nameSplited) {
                    if (int.TryParse(name, out _)) {
                        _log.Info($"Canal {textChannel.Name}");
                        numberChanged += 1;
                        await textChannel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, new OverwritePermissions(
                            PermValue.Inherit,
                            PermValue.Inherit,
                            PermValue.Inherit,
                            PermValue.Inherit,
                            PermValue.Inherit,
                            PermValue.Inherit,
                            PermValue.Allow
                        ));
                        break;
                    }
                }
            }

            await ReplyAsync($"mexi em {numberChanged} canais de texto");
        }




        [SlashCommand("getchannelinfo", "Get a channel name by id")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task GetTextChannelInfo(ulong channelId) {
            var channel = Context.Guild.GetChannel(channelId);
            if (channel == null) {
                await ReplyAsync("nao achei canal com esse id");
                return;
            }

            await ReplyAsync($"nome do canal: {channel.Name}");

        }

    }
}

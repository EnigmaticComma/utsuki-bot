using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using App.Extensions;
using RestSharp;

namespace App.Modules {

    [Name("Moderator")]
    [RequireContext(ContextType.Guild)]
    public class ModeratorModule : ModuleBase<SocketCommandContext> {

        #region <<---------- Properties and Fields ---------->>
        
        private readonly ModeratorService _moderatorService;
        private readonly LoggingService _log;
        private readonly DiscordSocketClient _discord;

        private Dictionary<ulong, DateTime> _lastChangedChannelsTimes = new();
        private TimeSpan _cooldownToChangeTeamName = TimeSpan.FromMinutes(1);

        #endregion <<---------- Properties and Fields ---------->>
        
        
        

        public ModeratorModule(DiscordSocketClient discord, ModeratorService moderatorService, LoggingService loggingService) {
            this._moderatorService = moderatorService;
            this._log = loggingService;
            this._discord = discord;
            this._discord.GuildAvailable += GuildAvailable;
        }

        public async Task GuildAvailable(SocketGuild socketGuild)
        {
            // Let's build a guild command! We're going to need a guild so lets just put that in a variable.

            // Next, lets create our slash command builder. This is like the embed builder but for slash commands.
            var guildCommand = new SlashCommandBuilder();

            // Note: Names have to be all lowercase and match the regular expression ^[\w-]{3,32}$
            guildCommand.WithName("say");

            // Descriptions can have a max length of 100.
            guildCommand.WithDescription("This is my first guild slash command!");

            // Let's do our global command
            var globalCommand = new SlashCommandBuilder();
            globalCommand.WithName("first-global-command");
            globalCommand.WithDescription("This is my first global slash command");

            try
            {
                // Now that we have our builder, we can call the CreateApplicationCommandAsync method to make our slash command.
                await socketGuild.CreateApplicationCommandAsync(guildCommand.Build());

                // With global commands we don't need the guild.
                await this._discord.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch(HttpException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                await this._log.Error(json);
            }
        }


        [Command("say"), Alias("s")]
        [Summary("Make the bot say something")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task Say(params string[] text) {
            if (text.Length <= 0) return;
            await this.Context.Message.DeleteAsync();
            await this.ReplyAsync(string.Join(' ', text));
        }

        [Group("set"), Name("Set commands")]
        [RequireContext(ContextType.Guild)]
        public class Set : ModuleBase {
            [Command("nick"), Priority(1)]
            [Summary("Change your nickname to the specified text")]
            [RequireUserPermission(GuildPermission.ChangeNickname)]
            public Task Nick([Remainder] string name) => this.Nick(this.Context.User as SocketGuildUser, name);

            [Command("nick"), Priority(0)]
            [Summary("Change another user's nickname to the specified text")]
            [RequireUserPermission(GuildPermission.ManageNicknames)]
            public async Task Nick(SocketGuildUser user, [Remainder] string name) {
                await user.ModifyAsync(x => x.Nickname = name);
                await this.ReplyAsync($"{user.Mention} I changed your name to **{name}**");
            }
        }

        [Command("kick")]
        [Summary("Kick the specified user.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task Kick([Remainder] SocketGuildUser user) {
            await this.ReplyAsync($"cya {user.Mention} :wave:");
            await user.KickAsync();
        }

        [Command("rc"), Alias("renamevoicechannel")]
        [Summary("Renames a voice channel that user is in.")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task RenameVoiceChannel(params string[] newNameArray) {
            if (newNameArray.Length <= 0) return;
            if (!(this.Context.User is SocketGuildUser user)) return;
            var vc = user.VoiceChannel;
            if (vc == null) return;
            var newName = string.Join(' ', newNameArray);
            newName = newName.FirstCharToUpper();
            await vc.ModifyAsync(p => p.Name = newName);
            await this.Context.Message.AddReactionAsync(new Emoji("✔"));
            await Task.Delay(TimeSpan.FromSeconds(5));
            await this.Context.Message.DeleteAsync();
        }

        [Command("randomimg")]
        public async Task GetRandomImg(int desiredResolution = 512) {
            var client = new RestClient();
            var timeline = await client.ExecuteAsync(new RestRequest($"https://picsum.photos/{desiredResolution}", Method.Get));

            var embed = new EmbedBuilder {
                Title = "Random image",
                Description = "from picsum.photos",
                ThumbnailUrl = timeline.ResponseUri.OriginalString
            };

            await this.ReplyAsync("", false, embed.Build());
        }

        [Command("getstatus")]
        [Summary("Get if user real status")]
        public async Task GetUserStatus(SocketGuildUser user) {
            var embed = new EmbedBuilder {
                Title = user.Status.ToString(),
                Description = $"Status de {MentionUtils.MentionUser(user.Id)}"
            };
            await this.ReplyAsync("", false, embed.Build());
        }

        [Command("deletelastmessages")]
        [Summary("Delete a number of messages in current channel.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task DeleteLastMessages(int limit) {
            await this._moderatorService.DeleteLastMessages(this.Context, limit);
        }



        [Command("newteam"), Alias("nteam")]
        [Summary("Creates a new team category, text and voice channel")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task CreateTeam(int order, params string[] name) {

            var teamName = string.Join(' ', name);
            var guild = this.Context.Guild;

            var category = await guild.CreateCategoryChannelAsync(teamName);
            await category.ModifyAsync(
                p => {
                    p.Position = (int) order;
                });

            var textChannel = await guild.CreateTextChannelAsync(teamName, p => { p.CategoryId = category.Id; });
            await textChannel.AddPermissionOverwriteAsync(this.Context.Guild.EveryoneRole, new OverwritePermissions(
                PermValue.Inherit,
                PermValue.Inherit,
                PermValue.Inherit,
                PermValue.Inherit,
                PermValue.Inherit,
                PermValue.Inherit,
                PermValue.Allow
            ));

            var voiceChannel = await guild.CreateVoiceChannelAsync(teamName, p => {
                p.CategoryId = category.Id;
                p.Bitrate = 32000;
            });

        }

        [Command("newcategory")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public Task CreateCategory(int pos = 1) {
            return this.CreateCategory(pos, new string[] {"Categoria"});
        }

        [Command("newcategory")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task CreateCategory(int pos, params string[] name) {
            var teamName = string.Join(' ', name);
            var guild = this.Context.Guild;
            var category = await guild.CreateCategoryChannelAsync(teamName);
            await category.ModifyAsync(p => p.Position = pos);
        }

        [Command("deleteallteams")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task DeleteAllTeams() {
            var guild = this.Context.Guild;

            var embed = new EmbedBuilder();
            IUserMessage msg = null;

            try {

                await this._log.Info($"Will delete all Teams in {guild.Name}");

                var allTextChannels = guild.TextChannels.Where(s => int.TryParse(s.Name.Split('-')[0], out _));
                var allVoiceChannles = guild.VoiceChannels.Where(s => int.TryParse(s.Name.Split('-')[0], out _));
                var allCategories = guild.CategoryChannels.Where(s => int.TryParse(s.Name.Split('-')[0], out _));

                await this._log.Info($"Texts {allTextChannels.Count()}, Voices {allVoiceChannles.Count()}, Cats {allCategories.Count()}");


                foreach (var c in allTextChannels) {
                    await Task.Delay(1000);
                    if (c == null) continue;
                    await this._log.Info($"Deleting {c.Name}");
                    await c.DeleteAsync();
                }
                foreach (var c in allVoiceChannles) {
                    await Task.Delay(1000);
                    if (c == null) continue;
                    await this._log.Info($"Deleting {c.Name}");
                    await c.DeleteAsync();
                }
                foreach (var c in allCategories) {
                    await Task.Delay(1000);
                    if (c == null) continue;
                    await this._log.Info($"Deleting {c.Name}");
                    await c.DeleteAsync();
                }

            } catch (Exception e) {
                embed.Title = "oh no";
                embed.Description = $"{this.Context.Guild.Owner.Mention} socorro nao consegui destruir tudo";
                embed.Footer = new EmbedFooterBuilder {
                    Text = e.Message.SubstringSafe(256)
                };
                embed.Color = Color.Red;

                await this._log.Error(e.ToString());

                if (msg != null) {
                    await msg.ModifyAsync(m => m.Embed = embed.Build());
                }
                else {
                    await this.ReplyAsync(string.Empty, false, embed.Build());
                }

            }
        }



        [Command("teamname")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task RenameTeam(params string[] name) {
            var json = JsonCache.LoadFromJson<JArray>("Moderation/guild-with-teams");
            if (!json.Any(v => v.Value<ulong>() == this.Context.Guild.Id)) return;

            if (!(this.Context.Channel is SocketTextChannel textChannel)) return;
            name ??= new[] {"equipe"};

            var embed = new EmbedBuilder();
            IUserMessage msg = null;

            if (this._lastChangedChannelsTimes.ContainsKey(this.Context.Channel.Id)) {
                var nextTime = this._lastChangedChannelsTimes[this.Context.Channel.Id] + this._cooldownToChangeTeamName;
                if (DateTime.UtcNow <= nextTime) {
                    embed.Title = "calma";
                    embed.Description = $"a equipe mudou o nome agora a pouco, espera mais uns {(DateTime.UtcNow - nextTime).TotalSeconds} segundos pra tentar denovo";
                    embed.Color = Color.Orange;
                    await this.ReplyAsync(string.Empty, false, embed.Build());
                    return;
                }
                else {
                    this._lastChangedChannelsTimes.Remove(this.Context.Channel.Id);
                }
            }

            try {
                // name
                var names = this.Context.Channel.Name.Split('-').ToList();
                if (names.Count == 1) {
                    names.Add("");
                }

                if (names.Count < 2 && !(int.TryParse(names[0], out var teamNumber))) {
                    throw new Exception("names count is lesser than 2 or first item is not a number");
                }
                names[1] = string.Join('-', name);

                var fullName = $"{names[0]}-{names[1]}";

                // answer
                embed.Title = "perai q eu so lenta";
                embed.Description = $"vo tentar mudar o nome da equipe pra '{fullName}'";
                embed.Color = Color.Blue;

                msg = await this.ReplyAsync(string.Empty, false, embed.Build());

                await textChannel.ModifyAsync(p => p.Name = fullName);
                await textChannel.Category.ModifyAsync(p => p.Name = fullName);

                foreach (var voiceChannel in this.Context.Guild.VoiceChannels) {
                    if (voiceChannel.Category != textChannel.Category) continue;
                    await voiceChannel.ModifyAsync(p => p.Name = fullName);
                    break;
                }

                // done
                embed.Title = "Pronto";
                embed.Description = $"troquei o nome da equipe pra **{fullName}**, {this.GetNameChangeAnswer(names[1])}";
                embed.Color = Color.Green;
                this._lastChangedChannelsTimes[this.Context.Channel.Id] = DateTime.UtcNow;
                await msg.ModifyAsync(m => m.Embed = embed.Build());

            } catch (Exception e) {
                embed.Title = "oh no";
                embed.Description = $"{this.Context.Guild.Owner.Mention} socorro nao entendi o q o {(this.Context.User as SocketGuildUser).GetNameSafe()} falou";
                embed.Footer = new EmbedFooterBuilder {
                    Text = e.Message.SubstringSafe(256)
                };
                embed.Color = Color.Red;

                await this._log.Error(e.ToString());

                if (msg != null) {
                    await msg.ModifyAsync(m => m.Embed = embed.Build());
                }
                else {
                    await this.ReplyAsync(string.Empty, false, embed.Build());
                }

            }
        }

        private string GetNameChangeAnswer(string teamName) {
            teamName = ChatService.RemoveDiacritics(teamName);
            teamName = teamName.ToLower()
                               .Replace(" ", string.Empty);

            if (teamName == "equipe") {
                return "nome padrão";
            }

            if (teamName == "rocket" || teamName == "equiperocket") {
                return "decolando pra lua";
            }

            if (teamName.Contains("naro") || teamName.Contains("taok") || teamName.Contains("bozo")) {
                return "ta ok";
            }

            if (teamName.Contains("studio")) {
                return "um studio melhor que CD Project Red";
            }

            if (teamName.Contains("team")) {
                return "team é equipe em ingles";
            }

            if (teamName.Contains("ufpr")) {
                return "e me deu saudade do RU";
            }

            if (teamName == "nomeaqui") {
                return "mas era pra vc digitar o nome da equipe no lugar de NOME AQUI";
            }


            var listOfDefaultAnswers = new[] {
                "mas nao gostei do nome",
                "mas achei q iam colocar outro nome",
                "mas eu queria outro nome",
                "um belo nome",
                "só gente bonita nessa equipe",
                "agora vao dormi",
                "agora vai beber agua",
                "mas continuo com fome",
                "igual o daquela outra equipe"
            };

            return listOfDefaultAnswers.RandomElement();
        }

        [Command("teampins")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetPins() {
            int numberChanged = 0;
            foreach (var textChannel in this.Context.Guild.TextChannels) {
                var nameSplited = textChannel.Name.Split('-');
                await this._log.Info($"Vendo canal {textChannel.Name}");
                foreach (var name in nameSplited) {
                    if (int.TryParse(name, out _)) {
                        await this._log.Info($"Canal {textChannel.Name}");
                        numberChanged += 1;
                        await textChannel.AddPermissionOverwriteAsync(this.Context.Guild.EveryoneRole, new OverwritePermissions(
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

            await this.ReplyAsync($"mexi em {numberChanged} canais de texto");
        }




        [Command("getchannelinfo")]
        [Summary("Get a channel name by id")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task GetTextChannelInfo(ulong channelId) {
            var channel = this.Context.Guild.GetChannel(channelId);
            if (channel == null) {
                await this.ReplyAsync("nao achei canal com esse id");
                return;
            }

            await this.ReplyAsync($"nome do canal: {channel.Name}");

        }

    }
}

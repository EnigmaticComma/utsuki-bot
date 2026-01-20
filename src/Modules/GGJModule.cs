using App.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace App.Modules;

public class GGJModule(GGJService _ggjService, LoggingService _log) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("teamname", "Set the team name")]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task RenameTeam(string name)
    {
        if(!_ggjService.CheckIfChannelCategoryIsTeam(Context.Channel)) return;
        await RespondAsync($"Definindo nome da equipe para {name}");
        await _ggjService.RenameTeam(name, Context.Guild, Context.Channel);
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

    [SlashCommand("organizeteams","organize team categories")]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    [RequireUserPermission(GuildPermission.ManageChannels)]
    public async Task OrganizeTeams()
    {
        _log.Info("Organizing teams");
        await RespondAsync("Organizing teams");
        var allCategories = Context.Guild.CategoryChannels.OrderBy(c=>c.Position).ToArray();

        SocketCategoryChannel? lastStaticCategory = null;
        int firstTeamIndex = -1;
        List<SocketCategoryChannel> teamsCategories = new();
        for (int i = 0; i < allCategories.Length; i++) {
            if(!char.IsDigit(allCategories[i].Name[0])) {
                lastStaticCategory = allCategories[i];
                _log.Info($"Last static category is '{lastStaticCategory.Name}' index is {i}, position is {allCategories[i].Position}");
                continue;
            }
            if(firstTeamIndex == -1) {
                _log.Info($"First team '{allCategories[i].Name}' index is {i}, position is {allCategories[i].Position}");
                firstTeamIndex = i;
            }
            teamsCategories.Add(allCategories[i]);
        }

        _log.Info($"List of team categories has {teamsCategories.Count} teams");

        if(lastStaticCategory == null) {
            await RespondAsync("Cant find last static category");
            return;
        }

        _log.Info($"Parsing and sorting teams");
        teamsCategories = teamsCategories.OrderBy(c => int.Parse(c.Name.Split('-')[0])).ToList();

        _log.Info($"Starting sorting teams");
        int position = lastStaticCategory.Position;
        foreach (var tc in teamsCategories) {
            position++;
            if(tc.Position == position) continue;
            _log.Info($"Will set position of {tc.Name} that is on {tc.Position} to {position}");
            await tc.ModifyAsync(c => c.Position = position);
        }
        
        await RespondAsync("Categories organized");
    }



    [SlashCommand("deleteallteams", "Delete all teams")]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task DeleteAllTeams()
    {
        await RespondAsync("Deleting all teams...", ephemeral: true);
        var guild = Context.Guild;
        var categories = guild.CategoryChannels;
        var teamCategories = categories.Where(c => char.IsDigit(c.Name[0])).ToList();

        _log.Info($"Found {teamCategories.Count} team categories to delete.");

        foreach (var category in teamCategories)
        {
            try 
            {
                _log.Info($"Deleting team category: {category.Name}");
                
                // Delete channels inside the category
                var channels = guild.Channels.Where(c => c is INestedChannel nested && nested.CategoryId == category.Id).ToList();

                var textChannels = channels.OfType<SocketTextChannel>().ToList();
                var voiceChannels = channels.OfType<SocketVoiceChannel>().ToList();
                var deletedIds = new HashSet<ulong>();

                foreach (var channel in textChannels)
                {
                    if (deletedIds.Contains(channel.Id)) continue;
                    try {
                        _log.Info($"Deleting text channel: {channel.Name} ({channel.Id})");
                        await channel.DeleteAsync();
                        deletedIds.Add(channel.Id);
                    } catch (Exception ex) {
                         _log.Error($"Failed to delete text channel {channel.Name}: {ex.Message}");
                    }
                }

                foreach (var channel in voiceChannels)
                {
                    if (deletedIds.Contains(channel.Id)) continue;
                    try {
                        _log.Info($"Deleting voice channel: {channel.Name} ({channel.Id})");
                        await channel.DeleteAsync();
                        deletedIds.Add(channel.Id);
                    } catch (Exception ex) {
                         // Ignore 10003 (Unknown Channel) as it likely means it was already deleted or doesn't exist
                         if (ex is Discord.Net.HttpException httpEx && httpEx.DiscordCode == DiscordErrorCode.UnknownChannel) {
                             _log.Info($"Channel {channel.Name} already deleted (Unknown Channel).");
                         } else {
                             _log.Error($"Failed to delete voice channel {channel.Name}: {ex.Message}");
                         }
                    }
                }

                await category.DeleteAsync();
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to delete category {category.Name}: {ex}");
            }
        }

        await ModifyOriginalResponseAsync(m => m.Content = $"Deleted {teamCategories.Count} teams.");
    }
}


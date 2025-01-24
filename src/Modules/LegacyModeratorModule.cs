using Discord;
using Discord.Commands;

namespace App.Modules;

public class LegacyModeratorModule : ModuleBase<SocketCommandContext>
{
    readonly ModeratorService _moderatorService;

    public LegacyModeratorModule(ModeratorService moderatorService)
    {
        _moderatorService = moderatorService;
    }

    [Command("teamname")]
    [Summary("Set the team name")]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task RenameTeam(params string[] names)
    {
        await _moderatorService.RenameTeam(string.Join(' ', names), Context.Guild, Context.Channel);
    }


}
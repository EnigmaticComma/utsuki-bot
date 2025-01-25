using App.Services;
using Discord;
using Discord.Commands;

namespace App.Modules.Legacy;

public class LegacyGGJModule(GGJService _ggjService) : ModuleBase<SocketCommandContext>
{
    [Command("teamname")]
    [Summary("Set the team name")]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task RenameTeam(params string[] names)
    {
        await _ggjService.RenameTeam(string.Join(' ', names), Context.Guild, Context.Channel);
    }
}
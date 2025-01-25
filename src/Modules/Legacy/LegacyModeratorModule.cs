using Discord;
using Discord.Commands;

namespace App.Modules.Legacy;

public class LegacyModeratorModule : ModuleBase<SocketCommandContext>
{
    readonly ModeratorService _moderatorService;

    public LegacyModeratorModule(ModeratorService moderatorService)
    {
        _moderatorService = moderatorService;
    }



}
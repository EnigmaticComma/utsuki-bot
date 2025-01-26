using App.Services;

namespace App.Modules;

using Discord;
using Discord.Interactions;

public class DiceModule(DiceService _diceService) : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("d20", "Rola um d20 e retorna o resultado.")]
    public async Task RollD20()
    {
        var (result, description) = _diceService.RollD20();

        var embedBuilder = new EmbedBuilder()
            .WithTitle("🎲 Rolagem de D20")
            .WithDescription($"Você rolou: **{result}**\n{description}")
            .WithColor(result == 1 ? Color.Red : result == 20 ? Color.Green : Color.Blue)
            .WithTimestamp(DateTimeOffset.Now);

        await RespondAsync(embed: embedBuilder.Build());
    }
}

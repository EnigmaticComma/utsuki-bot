using App.Services;
using Discord;
using Discord.Commands;

namespace App.Modules.Legacy;

public class LegacyDiceModule(DiceService _diceService) : ModuleBase<SocketCommandContext>
{
    [Command("d20")]
    [Summary("Rola um d20 e retorna o resultado")]
    public async Task RollD20()
    {
        var (result, description) = _diceService.RollD20();

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"🎲 Resultado: {result}")
            .WithDescription($"{description}".Trim())
            .WithFooter("Rolagem de d20")
            .WithColor(result == 1 ? Color.Red : result == 20 ? Color.Green : Color.Blue);

        if(!string.IsNullOrEmpty(description)) embedBuilder.WithDescription(description);

        await Context.Channel.SendMessageAsync(embed: embedBuilder.Build(), messageReference: Context.Message.Reference);
    }
}
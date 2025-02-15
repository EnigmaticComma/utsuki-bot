﻿using App.Services;
using Discord;
using Discord.Interactions;

namespace App.Modules;

public class DiceModule(DiceService _diceService) : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("d20", "Rola um d20 e retorna o resultado")]
    public async Task RollD20()
    {
        var (result, description) = _diceService.RollD20();

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"🎲 Resultado: {result}")
            .WithDescription($"{description}".Trim())
            .WithFooter("Rolagem de d20")
            .WithColor(result == 1 ? Color.Red : result == 20 ? Color.Green : Color.Blue);

        if(!string.IsNullOrEmpty(description)) embedBuilder.WithDescription(description);

        await RespondAsync(embed: embedBuilder.Build());
    }
}

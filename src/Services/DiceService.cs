using App.Attributes;

namespace App.Services;

[Service]
public class DiceService(Random _random)
{
    public (int result, string description) RollD20()
    {
        var roll = _random.Next(1, 21);
        if (roll == 1)
            return (roll, "Falha Crítica!");
        if (roll == 20)
            return (roll, "Sucesso Crítico!");
        return (roll, "");
    }
}
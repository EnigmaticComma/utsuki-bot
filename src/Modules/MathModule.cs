using Discord.Interactions;
using System.Linq;
using System.Threading.Tasks;

namespace App.Modules
{
    public class MathModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("isinteger", "Check if the input text is a whole number.")]
        public Task IsInteger(int number)
            => RespondAsync($"The text {number} is a number!");
        
        [SlashCommand("multiply", "Get the product of two numbers.")]
        public async Task Multiply(int a, int b)
        {
            int product = a * b;
            await RespondAsync($"The product of `{a} * {b}` is `{product}`.");
        }

        [SlashCommand("addmany", "Get the sum of many numbers (space separated)")]
        public async Task AddMany(string numbersString)
        {
            var numbers = numbersString.Split(' ').Select(s => int.TryParse(s, out var n) ? n : (int?)null).Where(n => n.HasValue).Select(n => n!.Value).ToArray();
            if (numbers.Length == 0) {
                await RespondAsync("Nenhum número válido fornecido.", ephemeral: true);
                return;
            }
            int sum = numbers.Sum();
            await RespondAsync($"The sum of `{string.Join(", ", numbers)}` is `{sum}`.");
        }
    }
}

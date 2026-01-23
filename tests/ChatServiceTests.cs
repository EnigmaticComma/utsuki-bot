using App.Services;
using Xunit;

namespace tests;

public class ChatServiceTests
{
    [Theory]
    [InlineData("São Paulo", "Sao Paulo")]
    [InlineData("Conceição", "Conceicao")]
    [InlineData("Áéíóú", "Aeiou")]
    [InlineData("çÇ", "cC")]
    public void RemoveDiacritics_ShouldRemoveAccents(string input, string expected)
    {
        var result = ChatService.RemoveDiacritics(input);
        Assert.Equal(expected, result);
    }
}

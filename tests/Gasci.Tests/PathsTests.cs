using Gasci.Core;

namespace Gasci.Tests;

public sealed class PathsTests
{
    [Theory]
    [InlineData("Relato", "Relato")]
    [InlineData("  My Game  ", "My Game")]
    [InlineData("Act 1: The Door?", "Act 1_ The Door_")]
    [InlineData("a/b\\c", "a_b_c")]
    [InlineData("Dots...", "Dots")]
    [InlineData("", "Gasci")]
    [InlineData("???", "___")]
    [InlineData(null, "Gasci")]
    public void Game_title_becomes_a_valid_folder_name(string? title, string expected) =>
        Assert.Equal(expected, Paths.ToFolderName(title));
}

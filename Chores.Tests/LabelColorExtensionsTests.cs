using Chores.Models;

namespace Chores.Tests;

public class LabelColorExtensionsTests
{
    [Theory]
    [InlineData("#f2db2e", "#111827")]
    [InlineData("#ffffff", "#111827")]
    [InlineData("#1f7ae0", "#ffffff")]
    [InlineData("#1f7", "#111827")]
    [InlineData("#155724", "#ffffff")]
    public void GetAccessibleTextColor_PicksHigherContrastColor(string backgroundColor, string expectedTextColor)
    {
        Assert.Equal(expectedTextColor, backgroundColor.GetAccessibleTextColor());
    }

    [Fact]
    public void Label_GetAccessibleTextColor_UsesLabelFillColor()
    {
        var label = new Label { Color = "#f2db2e" };

        Assert.Equal("#111827", label.GetAccessibleTextColor());
    }
}
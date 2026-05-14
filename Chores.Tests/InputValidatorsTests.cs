using Chores.Services;

namespace Chores.Tests;

public class InputValidatorsTests
{
    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("Alice.Smith", "Alice.Smith")]
    [InlineData(" user-name ", "user-name")]
    public void LoginNameValidator_TryNormalize_AcceptsExpectedValues(string input, string expected)
    {
        var isValid = LoginNameValidator.TryNormalize(input, out var normalized);

        Assert.True(isValid);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("bad name")]
    [InlineData("bad/name")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void LoginNameValidator_TryNormalize_RejectsInvalidValues(string input)
    {
        var isValid = LoginNameValidator.TryNormalize(input, out _);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("#6c757d")]
    [InlineData("#ABCDEF")]
    [InlineData(" #1f7 ")]
    public void LabelColorValidator_TryNormalize_AcceptsHexColors(string input)
    {
        var isValid = LabelColorValidator.TryNormalize(input, out _);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12")]
    [InlineData("#12345g")]
    [InlineData("123456")]
    public void LabelColorValidator_TryNormalize_RejectsInvalidColors(string input)
    {
        var isValid = LabelColorValidator.TryNormalize(input, out _);

        Assert.False(isValid);
    }
}

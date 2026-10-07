namespace Plugwright.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.2", "1.2.0", 0)]
    [InlineData("1.2.0+abc", "1.2.0", 0)]
    [InlineData("1.2.0.0", "1.2.0", 0)]
    [InlineData("1.2.0.1", "1.2.0", 1)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11", -1)]
    [InlineData("1.0.0-beta.11", "1.0.0-beta.rc", -1)]
    [InlineData("1.0.0-beta", "1.0.0-beta.1", -1)]
    public void Compares_by_semantic_version_precedence(string left, string right, int expected)
    {
        SemanticVersion.TryParse(left, out var leftVersion).ShouldBeTrue();
        SemanticVersion.TryParse(right, out var rightVersion).ShouldBeTrue();

        Math.Sign(leftVersion.CompareTo(rightVersion)).ShouldBe(expected);
        Math.Sign(rightVersion.CompareTo(leftVersion)).ShouldBe(-expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.-2.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0-beta..1")]
    [InlineData("1.0.0-be_ta")]
    public void Rejects_invalid_versions(string? text)
    {
        SemanticVersion.TryParse(text, out _).ShouldBeFalse();
    }
}

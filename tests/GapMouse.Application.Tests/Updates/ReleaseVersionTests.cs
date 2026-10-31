using GapMouse.Application.Updates;

namespace GapMouse.Application.Tests.Updates;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("V1.2", "1.2.0")]
    [InlineData("v1.2.3-beta.1", "1.2.3")]
    [InlineData("1.2.3+abc", "1.2.3")]
    public void TryParse_ValidTag_ReturnsNormalizedVersion(string text, string expected)
    {
        ReleaseVersion.TryParse(text, out var version).ShouldBeTrue();
        version.ShouldBe(new Version(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1")]
    [InlineData("1.2.3.4.5")]
    public void TryParse_InvalidTag_ReturnsFalse(string text)
    {
        ReleaseVersion.TryParse(text, out var version).ShouldBeFalse();
        version.ShouldBeNull();
    }

    [Fact]
    public void Normalize_TwoPartVersion_EqualsThreePart()
    {
        ReleaseVersion.Normalize(new Version(1, 2)).ShouldBe(new Version(1, 2, 0));
    }
}

using Angor.Shared.Models;
using Angor.Shared.Services;
using FluentAssertions;

namespace Angor.Test.Services;

/// <summary>
/// Kind 3030 is a draft/custom Nostr kind, not officially reserved, so public relays can contain
/// unrelated non-JSON content published by other applications under the same kind number.
/// <see cref="RelayService.LookupLatestProjects{T}"/> uses <see cref="RelayService.LooksLikeJsonObject"/>
/// to cheaply skip that noise (logged at Debug) instead of attempting a full JSON parse that fails
/// with a JsonException on every such event (previously logged at Warning with a full stack trace).
/// </summary>
public class RelayServiceTests
{
    [Fact]
    public void LooksLikeJsonObject_ReturnsTrue_ForSerializedProjectInfo()
    {
        var serializer = new Serializer();
        var json = serializer.Serialize(new ProjectInfo
        {
            FounderKey = "founder",
            FounderRecoveryKey = "recovery",
            ProjectIdentifier = "angor1test",
            NostrPubKey = "abc123",
            NetworkName = "Testnet"
        });

        RelayService.LooksLikeJsonObject(json).Should().BeTrue();
    }

    [Fact]
    public void LooksLikeJsonObject_ReturnsTrue_WhenContentHasLeadingWhitespace()
    {
        RelayService.LooksLikeJsonObject("  \n{\"version\":2}").Should().BeTrue();
    }

    [Fact]
    public void LooksLikeJsonObject_ReturnsFalse_ForForeignHexBlobContent()
    {
        // Real content observed from an unrelated Nostr event that reused kind 3030 on a public relay
        // (0x-prefixed hex blob), which previously threw a JsonException at byte position 1.
        const string foreignContent =
            "0xdf71595bf0776ae54a15c87844c79d0cff46f6fab1739a084cf04f50d4a700b6dbe5978a67ccfd02451918e5e1747939b8936fef48f37ac439e6d878af7b6de";

        RelayService.LooksLikeJsonObject(foreignContent).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("12345")]
    public void LooksLikeJsonObject_ReturnsFalse_ForNonObjectContent(string content)
    {
        RelayService.LooksLikeJsonObject(content).Should().BeFalse();
    }
}

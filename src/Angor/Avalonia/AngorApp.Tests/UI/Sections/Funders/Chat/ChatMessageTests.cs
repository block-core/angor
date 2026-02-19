using AngorApp.UI.Sections.Funders.Chat;
using FluentAssertions;

namespace AngorApp.Tests.UI.Sections.Funders.Chat;

public class ChatMessageTests
{
    [Fact]
    public void Constructor_with_explicit_id_should_preserve_it()
    {
        const string id = "nostr-event-id-123";

        var sut = new ChatMessage(id, "hello", true);

        sut.Id.Should().Be(id);
    }

    [Fact]
    public void Constructor_without_id_should_generate_unique_local_ids()
    {
        var first = new ChatMessage("same text", true);
        var second = new ChatMessage("same text", true);

        first.Id.Should().NotBeNullOrWhiteSpace();
        second.Id.Should().NotBeNullOrWhiteSpace();
        first.Id.Should().NotBe(second.Id);
    }
}

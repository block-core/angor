namespace AngorApp.UI.Sections.Funders.Chat;

public record ChatMessage(string Id, string Text, bool IsOwnMessage)
{
    public ChatMessage(string text, bool isOwnMessage) : this(Guid.NewGuid().ToString("N"), text, isOwnMessage)
    {
    }
}
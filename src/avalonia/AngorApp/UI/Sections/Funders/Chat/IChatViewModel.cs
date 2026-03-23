namespace AngorApp.UI.Sections.Funders.Chat;

public interface IChatViewModel
{
    IEnumerable<ChatMessage> Messages { get; }
    string CurrentText { get; set; }
    public IEnhancedCommand SendMessage { get; }
}
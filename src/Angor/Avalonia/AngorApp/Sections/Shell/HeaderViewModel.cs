using Zafiro.UI.Commands;

namespace AngorApp.Sections.Shell;

public class HeaderViewModel
{
    public IEnhancedCommand Back { get; }
    public object Content { get; }

    public HeaderViewModel(IEnhancedCommand back, object content)
    {
        Back = back;
        if (content is IHaveHeader headered)
        {
            Content = headered.Header;
        }
    }
}
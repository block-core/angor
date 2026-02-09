namespace AngorApp.UI.Shared.OperationResult
{
    public class OperationResultViewModel(string title, string text, object? icon = null) : IOperationResultViewModel
    {
        public string Title { get; } = title;
        public string Text { get; } = text;
        public object? Icon { get; } = icon;
        public Feeling Feeling { get; } = Feeling.Good;
    }
}
namespace AngorApp.UI.Shared.OperationResult
{
    public class OperationResultViewModel(string title, string text, object? icon = null, object? additionalContent = null) : IOperationResultViewModel
    {
        public string Title { get; } = title;
        public string Text { get; } = text;
        public object? Icon { get; } = icon;
        public object? AdditionalContent { get; } = additionalContent;
        public Feeling Feeling { get; set; } = Feeling.Good;
    }
}
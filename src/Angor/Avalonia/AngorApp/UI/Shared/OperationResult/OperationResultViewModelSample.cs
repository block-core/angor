namespace AngorApp.UI.Shared.OperationResult
{
    public class OperationResultViewModelSample : IOperationResultViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public object? Icon { get; set; }
        public object? AdditionalContent { get; }
        public Feeling Feeling { get; set; } = Feeling.Good;
    }
}
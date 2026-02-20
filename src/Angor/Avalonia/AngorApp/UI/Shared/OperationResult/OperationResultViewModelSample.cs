namespace AngorApp.UI.Shared.OperationResult
{
    public class OperationResultViewModelSample : IOperationResultViewModel
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public object? Icon { get; set; }
        public object? AdditionalContent { get; }
        public Feeling Feeling { get; set; } = Feeling.Good;
    }
}
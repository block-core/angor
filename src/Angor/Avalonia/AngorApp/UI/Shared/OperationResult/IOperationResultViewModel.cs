namespace AngorApp.UI.Shared.OperationResult
{
    internal interface IOperationResultViewModel
    {
        string Title { get; }
        string Text { get; }
        object? Icon { get; }
        object? AdditionalContent { get; }
        public Feeling Feeling { get; }
    }
}
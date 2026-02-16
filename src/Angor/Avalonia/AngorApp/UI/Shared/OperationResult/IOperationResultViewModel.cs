namespace AngorApp.UI.Shared.OperationResult
{
    internal interface IOperationResultViewModel
    {
        string Title { get; }
        string Text { get; }
        object Icon { get; }
        public Feeling Feeling { get; }
    }
}
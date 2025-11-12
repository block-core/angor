namespace AngorApp.UI.Shared.Controls.Common.Success;

public class SuccessViewModel(string message) : ISuccessViewModel
{
    public string Message { get; } = message;
}
namespace AngorApp.UI.Controls.Common.Success;

public class SuccessViewModel(string message) : ISuccessViewModel
{
    public string Message { get; } = message;
}
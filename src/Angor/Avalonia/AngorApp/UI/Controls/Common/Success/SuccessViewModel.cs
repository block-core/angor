namespace AngorApp.UI.Controls.Common.Success;

public interface ISuccessViewModel
{
    string Message { get; }
}

public class SuccessViewModelDesign : ISuccessViewModel
{
    public string Message { get; set; } = "";
}

public class SuccessViewModel(string message) : ISuccessViewModel
{
    public string Message { get; } = message;
}
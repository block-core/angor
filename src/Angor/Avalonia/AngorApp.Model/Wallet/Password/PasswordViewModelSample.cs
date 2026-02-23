namespace AngorApp.Model.Wallet.Password;

public class PasswordViewModelSample : IPasswordViewModel
{
    public string Text { get; set; } = string.Empty;
    public object? Icon { get; set; }
    public string Password { get; set; } = string.Empty;
}
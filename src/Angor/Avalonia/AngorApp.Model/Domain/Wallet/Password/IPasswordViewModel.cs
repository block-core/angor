namespace AngorApp.Model.Domain.Wallet.Password;

public interface IPasswordViewModel
{
    string Text { get; }
    object? Icon { get; }
    public string Password { get; set; }
}
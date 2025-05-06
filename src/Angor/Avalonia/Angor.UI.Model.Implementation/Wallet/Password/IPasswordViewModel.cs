namespace Angor.UI.Model.Implementation.Wallet.Password;

public interface IPasswordViewModel
{
    string Text { get; }
    object? Icon { get; }
    public string Password { get; set; }
}
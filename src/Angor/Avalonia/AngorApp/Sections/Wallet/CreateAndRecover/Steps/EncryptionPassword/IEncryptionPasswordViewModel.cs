namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.EncryptionPassword;

public interface IEncryptionPasswordViewModel
{
    public string? Password { get; set; }
    public string? PasswordConfirm { get; set; }
}
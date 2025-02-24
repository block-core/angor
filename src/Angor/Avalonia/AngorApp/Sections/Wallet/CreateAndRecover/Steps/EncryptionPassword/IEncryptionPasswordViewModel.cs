namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.EncryptionPassword;

public interface IEncryptionPasswordViewModel
{
    public string? EncryptionKey { get; set; }
    public string? PasswordConfirm { get; set; }
}
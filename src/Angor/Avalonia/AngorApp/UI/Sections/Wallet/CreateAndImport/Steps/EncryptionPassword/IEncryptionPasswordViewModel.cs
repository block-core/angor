namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.EncryptionPassword;

public interface IEncryptionPasswordViewModel
{
    public string? EncryptionKey { get; set; }
    public string? PasswordConfirm { get; set; }
}
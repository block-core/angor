namespace AngorApp.Sections.Wallet.Create.Step5;

public interface IEncryptionPasswordViewModel
{
    public string? Password { get; set; }
    public string? PasswordConfirm { get; set; }
}
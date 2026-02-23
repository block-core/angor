namespace Angor.Shared.Models;

public class SignData
{
    public string ProjectIdentifier { get; set; } = string.Empty;

    public string FounderRecoveryPrivateKey { get; set; } = string.Empty;
    
    public string NostrPrivateKey { get; set; } = string.Empty;
}
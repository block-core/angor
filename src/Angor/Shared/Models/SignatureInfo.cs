namespace Angor.Shared.Models;

public class SignatureInfo
{
    public string ProjectIdentifier { get; set; }

    public List<SignatureInfoItem> Signatures { get; set; } = new();
}
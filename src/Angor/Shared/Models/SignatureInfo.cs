namespace Angor.Shared.Models;

public class SignatureInfo
{
    public string ProjectIdentifier { get; set; } = string.Empty;
    public List<SignatureInfoItem> Signatures { get; set; } = new();
    public DateTime? TimeOfSignatureRequest { get; set; }
    public string? SignatureRequestEventId { get; set; }

    /// <summary>
    /// Specify whether this a release or recovery signature type.
    /// </summary>
    public SignatureInfoType SignatureType { get; set; }
}

public enum SignatureInfoType
{
    Release,
    Recovery
}
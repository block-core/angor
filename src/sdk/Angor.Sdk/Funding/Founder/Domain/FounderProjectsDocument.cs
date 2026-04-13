namespace Angor.Sdk.Funding.Founder.Domain;

/// <summary>
/// Persistence document for founder projects per wallet.
/// Analogous to <see cref="Angor.Sdk.Funding.Investor.Domain.InvestmentRecordsDocument"/>.
/// </summary>
public class FounderProjectsDocument
{
    /// <summary>The wallet ID that owns these projects. Used as the document key.</summary>
    public required string WalletId { get; set; }

    /// <summary>Projects the founder has created from this wallet.</summary>
    public List<FounderProjectRecord> Projects { get; set; } = new();
}

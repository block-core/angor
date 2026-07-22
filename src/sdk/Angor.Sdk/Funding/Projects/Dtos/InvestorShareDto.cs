namespace Angor.Sdk.Funding.Projects.Dtos;

/// <summary>
/// Represents a single investor's share in a project.
/// </summary>
/// <param name="InvestorPublicKey">The investor's public key (hex).</param>
/// <param name="InvestorNpub">The investor's Nostr npub, if available.</param>
/// <param name="TotalInvested">Total amount invested by this investor (in sats).</param>
/// <param name="SharePercentage">This investor's share of the total project investment (0-100).</param>
/// <param name="AmountClaimedByFounder">Amount from this investor's share that has been claimed/spent by the founder (in sats).</param>
/// <param name="ClaimedPercentage">Percentage of this investor's total that has been claimed by the founder (0-100).</param>
public record InvestorShareDto(
    string InvestorPublicKey,
    string InvestorNpub,
    long TotalInvested,
    double SharePercentage,
    long AmountClaimedByFounder,
    double ClaimedPercentage);

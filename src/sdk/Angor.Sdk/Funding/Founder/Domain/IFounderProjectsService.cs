using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Founder.Domain;

/// <summary>
/// Manages persistence of founder projects per wallet.
/// Analogous to <see cref="Angor.Sdk.Funding.Investor.Domain.IPortfolioService"/> for investors.
/// </summary>
public interface IFounderProjectsService
{
    /// <summary>Get all locally persisted founder project records for a wallet.</summary>
    Task<Result<List<FounderProjectRecord>>> GetByWalletId(string walletId);

    /// <summary>Add a project to the founder's local record (idempotent — skips duplicates).</summary>
    Task<Result> Add(string walletId, FounderProjectRecord project);

    /// <summary>Add multiple projects to the founder's local record (idempotent — skips duplicates).</summary>
    Task<Result> AddRange(string walletId, IEnumerable<FounderProjectRecord> projects);
}

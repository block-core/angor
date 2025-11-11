using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface IMempoolIndexerCalculationApi
{
    Task<ProjectIndexerData?> GetProjectByIdAsync(string projectId);
    Task<(string projectId, ProjectStats? stats)> GetProjectStatsAsync(string projectId);
    Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId);
    Task<ProjectInvestment?> GetInvestmentAsync(string projectId, string investorPubKey);
}

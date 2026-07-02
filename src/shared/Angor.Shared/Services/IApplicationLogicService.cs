using Angor.Shared.Models;

namespace Angor.Shared.Services
{
    public interface IApplicationLogicService
    {
        bool IsInvestmentWindowOpen(ProjectInfo? project);
        bool IsProjectFunded(long target, long current);

    }
}

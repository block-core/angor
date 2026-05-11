using Angor.Shared.Models;
using Angor.Primitives.Network;

namespace Angor.Shared.Services
{
    public interface IApplicationLogicService
    {
        bool IsInvestmentWindowOpen(ProjectInfo? project);
        bool IsProjectFunded(long target, long current);

    }
}


using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Shared.Services
{
    public interface IApplicationLogicService
    {
        bool IsInvestmentWindowOpen(ProjectInfo? project);
    }
}


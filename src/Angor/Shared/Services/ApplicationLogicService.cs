using System;
using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Shared.Services
{
    public class ApplicationLogicService : IApplicationLogicService
    {
        private readonly INetworkConfiguration _networkConfiguration;

        public ApplicationLogicService(INetworkConfiguration networkConfiguration)
        {
            _networkConfiguration = networkConfiguration;
        }

        public bool IsInvestmentWindowOpen(ProjectInfo? project)
        {
            if (project == null) return false;

            // on testnet we always allow to invest for testing purposes.
            var isTestnet = _networkConfiguration.GetNetwork().NetworkType == NetworkType.Testnet;
            var debugMode = _networkConfiguration.GetDebugMode();
            if (isTestnet && debugMode) return true;

            var now = DateTime.UtcNow;

            if (now <= project.EndDate) return true;

            return false;
        }

        public bool IsProjectFunded(long target, long current)
        {
            var feePercentage = _networkConfiguration.GetAngorInvestFeePercentage;

            // reduce the fee from the total amount;
            long angorFee = (target * feePercentage) / 100;
            target -= angorFee;

            if (current < target)
                return false;

            return true;
        }
    }
}

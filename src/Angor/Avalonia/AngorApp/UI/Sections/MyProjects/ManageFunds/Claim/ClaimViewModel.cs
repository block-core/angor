using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Shared.Models;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim
{
    public class ClaimViewModel : IClaimViewModel
    {
        private readonly IFounderAppService founderAppService;
        private readonly UIServices uiServices;
        private readonly IWalletContext walletContext;

        public ClaimViewModel(
            IFullProject project,
            IFounderAppService founderAppService,
            UIServices uiServices,
            IWalletContext walletContext
        )
        {
            this.founderAppService = founderAppService;
            this.uiServices = uiServices;
            this.walletContext = walletContext;
            Project = project;

            RefreshableCollection<IClaimStage, int> stagesCollection = new(GetStages, stage => stage.StageId);
            Load = stagesCollection.Refresh;
            Stages = stagesCollection.Items;
        }

        public IEnhancedCommand<Result<IEnumerable<IClaimStage>>> Load { get; }

        public IFullProject Project { get; }
        public IEnumerable<IClaimStage> Stages { get; }

        private Task<Result<IEnumerable<IClaimStage>>> GetStages()
        {
            return walletContext.Require()
                                .Bind(w => founderAppService
                                           .GetClaimableTransactions(
                                               new GetClaimableTransactions.GetClaimableTransactionsRequest(
                                                   w.Id,
                                                   Project.ProjectId))
                                           .Map(response => CreateStages(response.Transactions)));
        }

        private IEnumerable<IClaimStage> CreateStages(IEnumerable<ClaimableTransactionDto> claimableTransactions)
        {
            var transactions = claimableTransactions.ToList();

            return Project.ProjectType is Angor.Shared.Models.ProjectType.Fund or Angor.Shared.Models.ProjectType.Subscribe
                ? CreateDynamicStages(transactions)
                : CreateInvestmentStages(transactions);
        }

        private IEnumerable<IClaimStage> CreateInvestmentStages(List<ClaimableTransactionDto> transactions)
        {
            var transactionsByStage = transactions
                .GroupBy(t => t.StageId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return Project.Stages
                .OrderBy(s => s.Index)
                .Select(stage => CreateClaimStage(
                    stage.Index,
                    stage.ReleaseDate,
                    transactionsByStage.GetValueOrDefault(stage.Index, [])))
                .ToList();
        }

        private IEnumerable<IClaimStage> CreateDynamicStages(List<ClaimableTransactionDto> transactions)
        {
            // For Fund/Subscribe projects, StageId is per-investor-transaction (not a global stage index).
            // Group by DynamicReleaseDate (normalized to Date) since that defines the actual stage.
            var groupedByDate = transactions
                .Where(t => t.DynamicReleaseDate.HasValue)
                .GroupBy(t => t.DynamicReleaseDate!.Value.Date)
                .OrderBy(g => g.Key)
                .ToList();

            return groupedByDate
                .Select((group, index) => CreateClaimStage(
                    index,
                    group.Key,
                    group.ToList()))
                .ToList();
        }

        private IClaimStage CreateClaimStage(
            int stageId,
            DateTime? releaseDate,
            List<ClaimableTransactionDto> stageTransactions
        )
        {
            List<IClaimableTransaction> availableTransactions = stageTransactions
                                                                .Where(transaction =>
                                                                           transaction.ClaimStatus is ClaimStatus
                                                                               .Unspent or ClaimStatus.Locked)
                                                                .Select(IClaimableTransaction (transaction) =>
                                                                            new ClaimableTransaction(transaction))
                                                                .ToList();

            DateTimeOffset claimableOn = GetClaimableOn(releaseDate, stageTransactions);
            FundsAvailability fundsAvailability = GetFundsAvailability(stageTransactions, availableTransactions.Count);

            return new ClaimStage(
                Project.ProjectId,
                stageId,
                availableTransactions,
                claimableOn,
                fundsAvailability,
                uiServices,
                founderAppService,
                walletContext);
        }

        private static DateTimeOffset GetClaimableOn(
            DateTime? releaseDate,
            IEnumerable<ClaimableTransactionDto> stageTransactions
        )
        {
            if (releaseDate.HasValue)
            {
                return new DateTimeOffset(releaseDate.Value);
            }

            DateTime? dynamicReleaseDate = stageTransactions
                                           .Select(transaction => transaction.DynamicReleaseDate)
                                           .FirstOrDefault(releaseDate => releaseDate.HasValue);

            return dynamicReleaseDate.HasValue
                ? new DateTimeOffset(dynamicReleaseDate.Value)
                : DateTimeOffset.Now;
        }

        private static FundsAvailability GetFundsAvailability(
            IEnumerable<ClaimableTransactionDto> stageTransactions,
            int availableTransactionCount
        )
        {
            if (availableTransactionCount > 0)
            {
                return FundsAvailability.FundsAvailable;
            }

            return stageTransactions.Any(transaction => transaction.ClaimStatus == ClaimStatus.SpentByFounder)
                ? FundsAvailability.SpentByFounder
                : FundsAvailability.Invalid;
        }
    }
}

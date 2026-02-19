using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
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
            List<IGrouping<int, ClaimableTransactionDto>> groupedTransactions = claimableTransactions
                .GroupBy(transaction => transaction.StageId)
                .ToList();

            List<IStage> projectStages = Project.Stages.ToList();

            IEnumerable<IClaimStage> projectStagesWithTransactions =
                from projectStage in projectStages
                join transactionGroup in groupedTransactions
                    on projectStage.Index equals transactionGroup.Key
                    into transactionsByStage
                from transactionGroup in transactionsByStage.DefaultIfEmpty()
                select CreateClaimStage(
                    projectStage.Index,
                    projectStage.ReleaseDate,
                    transactionGroup?.ToList() ?? []);

            return projectStagesWithTransactions.OrderBy(stage => stage.StageId).ToList();
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
                uiServices);
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

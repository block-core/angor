using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Stage;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Claim
{
    public class ClaimViewModel : IClaimViewModel
    {
        private readonly IFounderAppService founderAppService;
        private readonly UIServices uiServices;
        private readonly IWalletContext walletContext;

        public ClaimViewModel(
            IFundProject project,
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

        public IFundProject Project { get; }
        public IEnumerable<IClaimStage> Stages { get; }

        private async Task<Result<IEnumerable<IClaimStage>>> GetStages()
        {
            var wallet = await walletContext.Require();
            if (wallet.IsFailure)
            {
                return Result.Failure<IEnumerable<IClaimStage>>(wallet.Error);
            }

            var claimableResult = await founderAppService
                .GetClaimableTransactions(
                    new GetClaimableTransactions.GetClaimableTransactionsRequest(
                        wallet.Value.Id,
                        Project.Id));

            if (claimableResult.IsFailure)
            {
                return Result.Failure<IEnumerable<IClaimStage>>(claimableResult.Error);
            }

            return Result.Success(CreateDynamicStages(claimableResult.Value.Transactions));
        }

        private IEnumerable<IClaimStage> CreateDynamicStages(IEnumerable<ClaimableTransactionDto> claimableTransactions)
        {
            var transactions = claimableTransactions.ToList();

            var groupedByDate = transactions
                .Where(t => t.DynamicReleaseDate.HasValue)
                .GroupBy(t => t.DynamicReleaseDate!.Value.Date)
                .OrderBy(g => g.Key)
                .ToList();

            return groupedByDate
                .Select((group, index) => CreateClaimStage(
                    index,
                    new DateTimeOffset(group.Key),
                    group.ToList()))
                .ToList();
        }

        private IClaimStage CreateClaimStage(
            int stageId,
            DateTimeOffset claimableOn,
            List<ClaimableTransactionDto> stageTransactions
        )
        {
            List<ITransaction> availableTransactions = stageTransactions
                                                                .Where(transaction =>
                                                                           transaction.ClaimStatus is ClaimStatus
                                                                               .Unspent or ClaimStatus.Locked)
                                                                .Select(ITransaction (transaction) =>
                                                                            new Transaction(transaction))
                                                                .ToList();

            FundsAvailability fundsAvailability = GetFundsAvailability(stageTransactions, availableTransactions.Count);

            return new ClaimStage(
                Project.Id,
                stageId,
                availableTransactions,
                claimableOn,
                fundsAvailability,
                uiServices,
                founderAppService,
                walletContext);
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

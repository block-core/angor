using System.Reactive.Threading.Tasks;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Stage;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;
using InvestmentStage = AngorApp.Model.ProjectsV2.InvestmentProject.IStage;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim
{
    public class ClaimViewModel : IClaimViewModel
    {
        private readonly IFounderAppService founderAppService;
        private readonly UIServices uiServices;
        private readonly IWalletContext walletContext;
        private static readonly TimeSpan SnapshotTimeout = TimeSpan.FromMilliseconds(500);

        public ClaimViewModel(
            IInvestmentProject project,
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

        public IInvestmentProject Project { get; }
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

            return await CreateStages(claimableResult.Value.Transactions);
        }

        private async Task<Result<IEnumerable<IClaimStage>>> CreateStages(IEnumerable<ClaimableTransactionDto> claimableTransactions)
        {
            var transactions = claimableTransactions.ToList();

            var stages = await SnapshotOrFallback(Project.Stages);
            if (stages is null)
            {
                return Result.Failure<IEnumerable<IClaimStage>>("Cannot load investment stages.");
            }

            return Result.Success(CreateInvestmentStages(transactions, stages));
        }

        private static async Task<T?> SnapshotOrFallback<T>(IObservable<T> observable)
        {
            var valueTask = observable.FirstAsync().ToTask();
            var timeoutTask = Task.Delay(SnapshotTimeout);
            var completedTask = await Task.WhenAny(valueTask, timeoutTask);
            if (completedTask != valueTask)
            {
                return default;
            }

            try
            {
                return await valueTask;
            }
            catch
            {
                return default;
            }
        }

        private IEnumerable<IClaimStage> CreateInvestmentStages(List<ClaimableTransactionDto> transactions, IReadOnlyCollection<InvestmentStage> stages)
        {
            var transactionsByStage = transactions
                .GroupBy(t => t.StageId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return stages
                .OrderBy(s => s.Id)
                .Select(stage => CreateClaimStage(
                    stage.Id,
                    stage.ReleaseDate,
                    transactionsByStage.GetValueOrDefault(stage.Id, [])))
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
                    new DateTimeOffset(group.Key),
                    group.ToList()))
                .ToList();
        }

        private IClaimStage CreateClaimStage(
            int stageId,
            DateTimeOffset? releaseDate,
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

            DateTimeOffset claimableOn = GetClaimableOn(releaseDate, stageTransactions);
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

        private static DateTimeOffset GetClaimableOn(
            DateTimeOffset? releaseDate,
            IEnumerable<ClaimableTransactionDto> stageTransactions
        )
        {
            if (releaseDate.HasValue)
            {
                return releaseDate.Value;
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

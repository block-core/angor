using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Dtos;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.Model.Shared.Services;
using Zafiro.Reactive;
using IStage = AngorApp.Model.ProjectsV2.InvestmentProject.IStage;

namespace AngorApp.Model.Funded.Investment.Model
{
    public class InvestmentFunded : FundedBase, IInvestmentFunded
    {
        public InvestmentFunded(
            IInvestmentProject project,
            IInvestmentInvestorData investorData,
            IFundedCommandsFactory fundedCommandsFactory
        ) : base(project, investorData, fundedCommandsFactory)
        {
            StagesWithStatus = project.Stages
                .CombineLatest(investorData.StageItems, ApplyStatuses)
                .ReplayLastActive();
        }

        public new IInvestmentProject Project => (IInvestmentProject)base.Project;
        public new IInvestmentInvestorData InvestorData => (IInvestmentInvestorData)base.InvestorData;
        public IObservable<IReadOnlyCollection<IStage>> StagesWithStatus { get; }

        IProject IFunded.Project => base.Project;
        IInvestorData IFunded.InvestorData => base.InvestorData;

        private static IReadOnlyCollection<IStage> ApplyStatuses(
            IReadOnlyCollection<IStage> stages,
            IReadOnlyList<InvestorStageItemDto> recoveryItems)
        {
            if (recoveryItems.Count == 0)
                return stages;

            var stagesByIndex = stages.ToDictionary(s => s.Id);

            return recoveryItems
                .OrderBy(item => item.StageIndex)
                .Select(item =>
                {
                    var hasStage = stagesByIndex.TryGetValue(item.StageIndex, out var stage);
                    return (IStage)new Stage(
                        id: item.StageIndex,
                        releaseDate: hasStage ? stage!.ReleaseDate : DateTimeOffset.MinValue,
                        ratio: hasStage ? stage!.Ratio : 0m,
                        total: new AmountUI(item.Amount),
                        status: !string.IsNullOrEmpty(item.Status) ? item.Status : "Pending"
                    );
                }).ToList();
        }
    }
}

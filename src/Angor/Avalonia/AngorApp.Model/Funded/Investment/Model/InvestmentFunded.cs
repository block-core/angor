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
            INotificationService notificationService,
            ITransactionDraftPreviewer draftPreviewer,
            IInvestmentAppService appService,
            IWalletContext walletContext
        ) : base(project, investorData, notificationService, draftPreviewer, appService, walletContext)
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

            var itemsByIndex = recoveryItems.ToDictionary(i => i.StageIndex);

            return stages.Select(stage =>
            {
                var updatedStage = stage;

                if (itemsByIndex.TryGetValue(stage.Id, out var item))
                {
                    if (stage is Stage s)
                    {
                        updatedStage = s.WithTotal(new AmountUI(item.Amount));

                        if (!string.IsNullOrEmpty(item.Status))
                        {
                            updatedStage = ((Stage)updatedStage).WithStatus(item.Status);
                        }
                    }
                }

                return updatedStage;
            }).ToList();
        }
    }
}

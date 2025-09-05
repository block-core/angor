using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

using System;
using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Services;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

public class ManageInvestorProjectViewModel : ReactiveObject, IManageInvestorProjectViewModel, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    public ManageInvestorProjectViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        ViewTransaction = ReactiveCommand.Create(() => { }).Enhance();

        Load = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("You need to create a wallet first."))
                .Bind(wallet => investmentAppService.GetInvestorProjectRecovery(wallet.Id.Value, projectId))
                .Tap(dto => Apply(dto));
        }).Enhance().DisposeWith(disposables);

        Load.HandleErrorsWith(uiServices.NotificationService, "Failed to load recovery info").DisposeWith(disposables);

        // Auto-load on create
        Load.Execute().Subscribe().DisposeWith(disposables);
    }

    private void Apply(InvestorProjectRecoveryDto dto)
    {
        Project = new InvestedProject(dto);
        Items = dto.Items
            .Select(x => (IInvestorProjectItem)new InvestorProjectItem(x.StageIndex + 1, new AmountUI(x.Amount), x.Status))
            .ToList();
        this.RaisePropertyChanged(nameof(Project));
        this.RaisePropertyChanged(nameof(Items));
    }

    public IAmountUI TotalFunds => Project.TotalFunds;
    public IEnhancedCommand ViewTransaction { get; }
    public DateTime ExpiryDate => Project.ExpiryDate;
    public TimeSpan PenaltyPeriod => Project.PenaltyPeriod;

    public IEnumerable<IInvestorProjectItem> Items { get; private set; } = Array.Empty<IInvestorProjectItem>();

    public IInvestedProject Project { get; private set; } = new InvestedProjectDesign();

    public IEnhancedCommand<Result<InvestorProjectRecoveryDto>> Load { get; }

    public void Dispose()
    {
        disposables.Dispose();
    }

    private class InvestorProjectItem(int stage, IAmountUI amount, string status) : IInvestorProjectItem
    {
        public int Stage { get; } = stage;
        public IAmountUI Amount { get; } = amount;
        public string Status { get; } = status;
    }

    private class InvestedProject : IInvestedProject
    {
        public InvestedProject(InvestorProjectRecoveryDto dto)
        {
            TotalFunds = new AmountUI(dto.TotalSpendable);
            ExpiryDate = dto.ExpiryDate;
            PenaltyPeriod = TimeSpan.FromDays(dto.PenaltyDays);
            Name = dto.Name ?? dto.ProjectIdentifier;
        }

        public IAmountUI TotalFunds { get; }
        public DateTime ExpiryDate { get; }
        public TimeSpan PenaltyPeriod { get; }
        public string Name { get; }
    }
}

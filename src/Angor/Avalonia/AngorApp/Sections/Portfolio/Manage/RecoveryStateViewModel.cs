using System.Linq;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Core;
using AngorApp.TransactionDrafts;
using AngorApp.TransactionDrafts.DraftTypes;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Portfolio.Manage;

public sealed record RecoveryStateViewModel
{
    private readonly InvestorProjectRecoveryDto dto;
    private readonly IInvestmentAppService investmentAppService;
    private readonly UIServices uiServices;

    public RecoveryStateViewModel(WalletId WalletId, InvestorProjectRecoveryDto dto, SharedCommands sharedCommands, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.dto = dto;
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;
        this.WalletId = WalletId;
        
        ViewTransaction = sharedCommands.OpenTransaction(dto.TransactionId);
        
        Project = new InvestedProject(dto);
        Stages = dto.Items
            .Select(IInvestorProjectStage (x) => new InvestorProjectStage(
                stage: x.StageIndex + 1,
                amount: new AmountUI(x.Amount),
                isSpent: x.IsSpent,
                status: x.Status))
            .ToList();

        BatchAction = CreateBatchCommand(this);
    }
    
     private IEnhancedCommand<Maybe<Guid>> CreateBatchCommand(RecoveryStateViewModel recoveryStateViewModel)
    {
        if (recoveryStateViewModel.CanRecover)
        {
            return ReactiveCommand.CreateFromTask(() => Recover(recoveryStateViewModel)).Enhance("Recover Funds");
        }
        
        if (recoveryStateViewModel.CanRelease)
        {
            return ReactiveCommand.CreateFromTask(() => Release(recoveryStateViewModel)).Enhance("Release Funds");
        }

        if (recoveryStateViewModel.CanClaim)
        {
            var buttonText = recoveryStateViewModel.IsBelowPenaltyThreshold
                ? "Claim Funds (Below Threshold)"
                : "Claim Funds";
            return ReactiveCommand.CreateFromTask(() => Claim(recoveryStateViewModel)).Enhance(buttonText);
        }

        return ReactiveCommand.Create(() => Maybe<Guid>.None, Observable.Return(false)).Enhance();
    }

    private Task<Maybe<Guid>> Recover(RecoveryStateViewModel recoveryStateViewModel)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(fr =>
        {
            return investmentAppService.BuildRecoverInvestorFunds(recoveryStateViewModel.WalletId.Value, Project.ProjectId, new DomainFeerate(fr))
                .Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel((InvestmentDraft)draft, uiServices));
        }, model => investmentAppService.SubmitTransactionFromDraft(recoveryStateViewModel.WalletId.Value, model.Model)
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds recovery transaction has been submitted successfully"))
            .Map(_ => Guid.Empty), uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }
    
    private Task<Maybe<Guid>> Claim(RecoveryStateViewModel recoveryStateViewModel)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(fr =>
        {
            return investmentAppService.BuildClaimInvestorEndOfProjectFunds(recoveryStateViewModel.WalletId.Value, Project.ProjectId, new DomainFeerate(fr))
                .Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel((InvestmentDraft)draft, uiServices));
        }, model => investmentAppService.SubmitTransactionFromDraft(recoveryStateViewModel.WalletId.Value, model.Model)
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds claim transaction has been submitted successfully"))
            .Map(_ => Guid.Empty), uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }
    
    private Task<Maybe<Guid>> Release(RecoveryStateViewModel recoveryStateViewModel)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(fr =>
        {
            return investmentAppService.BuildReleaseInvestorFunds(recoveryStateViewModel.WalletId.Value, Project.ProjectId, new DomainFeerate(fr))
                .Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel((InvestmentDraft)draft, uiServices));
        }, model => investmentAppService.SubmitTransactionFromDraft(recoveryStateViewModel.WalletId.Value, model.Model)
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds claim transaction has been submitted successfully"))
            .Map(_ => Guid.Empty), uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }

    public IEnhancedCommand<Maybe<Guid>> BatchAction { get; }

    public IEnhancedCommand ViewTransaction { get; }

    public List<IInvestorProjectStage> Stages { get;  }

    public InvestedProject Project { get; }

    public bool CanRecover => dto.CanRecover;
    public bool CanRelease => dto.CanRelease;
    public bool CanClaim => Stages.Any(stage => !stage.IsSpent);
    public bool IsBelowPenaltyThreshold => dto.CanClaim && !dto.EndOfProject;
    public WalletId WalletId { get; }

    public string TransactionId => dto.TransactionId; 
}
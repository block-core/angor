using System.Linq;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Sdk.Wallet.Domain;
using AngorApp.Core;
using AngorApp.UI.TransactionDrafts;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using AngorApp.UI.TransactionDrafts.DraftTypes.Base;
using AngorApp.UI.TransactionDrafts.DraftTypes.Investment;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Sections.Portfolio.Manage;

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
        
        GetNsec = ReactiveCommand.CreateFromTask(ShowInvestorNsecAsync).Enhance();
    }
    
    private async Task ShowInvestorNsecAsync()
    {
        var result = await investmentAppService.GetInvestorNsec(new GetInvestorNsec.GetInvestorNsecRequest(WalletId, dto.FounderKey));
        
        if (result.IsSuccess)
        {
            var copyableMessage = new ClipboardText($"Your investment private key (nsec):\n\n{result.Value.Nsec}\n\nSelect and copy the key above.");
            await uiServices.Dialog.ShowOk(copyableMessage, "Investor nsec");
        }
        else
        {
            await uiServices.Dialog.ShowOk("Error", $"Failed to retrieve nsec: {result.Error}");
        }
    }

    private IEnhancedCommand<Maybe<Guid>> CreateBatchCommand(RecoveryStateViewModel recoveryStateViewModel)
    {
        if (recoveryStateViewModel.CanClaimReleasedFunds)
        {
            return ReactiveCommand.CreateFromTask(() => ClaimUnfundedRelease(recoveryStateViewModel)).Enhance("Claim Released Funds");
        }

        if (recoveryStateViewModel.CanSpendEndOfProjectOrThreshold)
        {
            var buttonText = recoveryStateViewModel.IsBelowPenaltyThreshold
                ? "Claim Funds (Below Threshold)"
                : "Claim Funds";
            return ReactiveCommand.CreateFromTask(() => Claim(recoveryStateViewModel)).Enhance(buttonText);
        }

        if (recoveryStateViewModel.CanRecoverToPenalty)
        {
            return ReactiveCommand.CreateFromTask(() => Recover(recoveryStateViewModel)).Enhance("Recover Funds");
        }

        if (recoveryStateViewModel.CanReleaseFromPenalty)
        {
            return ReactiveCommand.CreateFromTask(() => ReleasePenalty(recoveryStateViewModel)).Enhance("Release Funds");
        }

        return ReactiveCommand.Create(() => Maybe<Guid>.None, Observable.Return(false)).Enhance();
    }

    private Task<Maybe<Guid>> Recover(RecoveryStateViewModel recoveryStateViewModel)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
        fr =>
        {
            return investmentAppService.BuildRecoveryTransaction(new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(recoveryStateViewModel.WalletId, Project.ProjectId, new DomainFeerate(fr)))
                .Map(ITransactionDraftViewModel (response) => new TransactionDraftViewModel(response.TransactionDraft, uiServices));
        }, 
        model => 
        {
            return investmentAppService.SubmitTransactionFromDraft(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(recoveryStateViewModel.WalletId.Value, Project.ProjectId, model.Model))
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds recovery transaction has been submitted successfully"))
            .Map(_ => Guid.Empty);
        }, 
        uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }
    
    private Task<Maybe<Guid>> Claim(RecoveryStateViewModel recoveryStateViewModel)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
        fr =>
        {
            return investmentAppService.BuildEndOfProjectClaim(new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(recoveryStateViewModel.WalletId, Project.ProjectId, new DomainFeerate(fr)))
                .Map(ITransactionDraftViewModel (response) => new TransactionDraftViewModel(response.TransactionDraft, uiServices));
        }, 
        model => 
        {
            return investmentAppService.SubmitTransactionFromDraft(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(recoveryStateViewModel.WalletId.Value, Project.ProjectId, model.Model))
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds claim transaction has been submitted successfully"))
            .Map(_ => Guid.Empty);
        }, 
        uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }
    
    private Task<Maybe<Guid>> ClaimUnfundedRelease(RecoveryStateViewModel recoveryStateViewModel)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
        fr =>
        {
            return investmentAppService.BuildUnfundedReleaseTransaction(new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(recoveryStateViewModel.WalletId, Project.ProjectId, new DomainFeerate(fr)))
                .Map(ITransactionDraftViewModel (response) => new TransactionDraftViewModel(response.TransactionDraft, uiServices));
        }, 
        model =>
        {
            return investmentAppService.SubmitTransactionFromDraft(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(recoveryStateViewModel.WalletId.Value, Project.ProjectId, model.Model))
                .Tap(_ => uiServices.Dialog.ShowOk("Success", "Released funds have been claimed successfully"))
                .Map(_ => Guid.Empty);
        }, 
        uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Claim Released Funds", s => s.CommitDraft.Enhance("Claim Released Funds"));
    }
    
    private Task<Maybe<Guid>> ReleasePenalty(RecoveryStateViewModel recoveryStateViewModel)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
        fr =>
        {
            return investmentAppService.BuildPenaltyReleaseTransaction(new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(recoveryStateViewModel.WalletId, Project.ProjectId, new DomainFeerate(fr)))
                .Map(ITransactionDraftViewModel (response) => new TransactionDraftViewModel(response.TransactionDraft, uiServices));
        }, 
        model =>
        {
            return investmentAppService.SubmitTransactionFromDraft(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(recoveryStateViewModel.WalletId.Value, Project.ProjectId, model.Model))
                .Tap(_ => uiServices.Dialog.ShowOk("Success", "Penalty release transaction has been submitted successfully"))
                .Map(_ => Guid.Empty);
        }, 
        uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Release Funds", s => s.CommitDraft.Enhance("Release Funds"));
    }

    public IEnhancedCommand<Maybe<Guid>> BatchAction { get; }
    
    public IEnhancedCommand GetNsec { get; }

    public IEnhancedCommand ViewTransaction { get; }

    public List<IInvestorProjectStage> Stages { get;  }

    public InvestedProject Project { get; }

    public bool CanClaimReleasedFunds => dto.HasUnspentItems && dto.HasReleaseSignatures;
    public bool CanRecoverToPenalty => dto.HasUnspentItems && !dto.HasItemsInPenalty;
    public bool CanReleaseFromPenalty => dto.HasItemsInPenalty;
    public bool CanSpendEndOfProjectOrThreshold => dto.HasUnspentItems && (dto.EndOfProject || !dto.IsAboveThreshold);
    public bool EndOfProject => dto.EndOfProject;

    public bool IsBelowPenaltyThreshold => !dto.IsAboveThreshold;
    public WalletId WalletId { get; }

    public string TransactionId => dto.TransactionId; 
}
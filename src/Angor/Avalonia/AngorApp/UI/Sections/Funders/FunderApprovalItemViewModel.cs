using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Shared.Services;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Funders;

public class FunderApprovalItemViewModel : ReactiveObject, IFunderApprovalItemViewModel
{
    private readonly Func<Task> refresh;

    public FunderApprovalItemViewModel(
        ProjectId projectId,
        Investment investment,
        IWallet wallet,
        IFounderAppService founderAppService,
        UIServices uiServices,
        Func<Task> refresh)
    {
        this.refresh = refresh;
        Investment = investment;

        ProjectName = projectId.Value;
        AmountText = $"{investment.Amount / 100_000_000m:0.00000} BTC";
        Timestamp = investment.CreatedOn;

        Approve = ReactiveCommand.CreateFromTask(async () =>
        {
            var confirmationResult = await uiServices.Dialog.ShowConfirmation("Approve investment", "Do you want to approve this investment?");

            if (confirmationResult.HasNoValue || !confirmationResult.Value)
            {
                return;
            }

            var approvalResult = await founderAppService.ApproveInvestment(
                new ApproveInvestment.ApproveInvestmentRequest(wallet.Id, projectId, investment));

            if (approvalResult.IsFailure)
            {
                await uiServices.NotificationService.Show($"Could not approve investment: {approvalResult.Error}", "Error");
                return;
            }

            await this.refresh();
        }).Enhance();

        Reject = ReactiveCommand.Create(() => { }).Enhance();
        Message = ReactiveCommand.Create(() => { }).Enhance();
    }

    public Investment Investment { get; }

    public bool IsApproved => Investment.Status == InvestmentStatus.FounderSignaturesReceived || Investment.Status == InvestmentStatus.Invested;

    public string ProjectName { get; }

    public string AmountText { get; }

    public DateTimeOffset Timestamp { get; }

    public IEnhancedCommand Approve { get; }

    public IEnhancedCommand Reject { get; }

    public IEnhancedCommand Message { get; }
}

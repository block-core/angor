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
    private readonly ProjectId projectId;
    private readonly IWallet wallet;
    private readonly IFounderAppService founderAppService;
    private readonly UIServices uiServices;

    public FunderApprovalItemViewModel(
        ProjectId projectId,
        string projectName,
        Investment investment,
        IWallet wallet,
        IFounderAppService founderAppService,
        UIServices uiServices,
        Func<Task> refresh)
    {
        this.refresh = refresh;
        this.projectId = projectId;
        this.wallet = wallet;
        this.founderAppService = founderAppService;
        this.uiServices = uiServices;

        Investment = investment;

        ProjectName = projectName;
        AmountText = $"{investment.Amount / 100_000_000m:0.00000} BTC";
        Timestamp = investment.CreatedOn;

        Approve = ReactiveCommand.CreateFromTask(async () => await ApproveAsync()).Enhance();

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

    public async Task<bool> ApproveAsync()
    {
        var confirmationResult = await uiServices.Dialog.ShowConfirmation("Approve investment", "Do you want to approve this investment?");

        if (confirmationResult.HasNoValue || !confirmationResult.Value)
        {
            return false;
        }

        var approvalResult = await founderAppService.ApproveInvestment(
            new ApproveInvestment.ApproveInvestmentRequest(wallet.Id, projectId, Investment));

        if (approvalResult.IsFailure)
        {
            await uiServices.NotificationService.Show($"Could not approve investment: {approvalResult.Error}", "Error");
            return false;
        }

        await refresh();
        return true;
    }
}

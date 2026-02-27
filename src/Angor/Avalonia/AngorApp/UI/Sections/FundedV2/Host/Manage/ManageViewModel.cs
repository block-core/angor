using System.Reactive.Disposables;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using AngorApp.UI.Sections.FundedV2.Common.Model;
using Zafiro.Reactive;

namespace AngorApp.UI.Sections.FundedV2.Host.Manage;

public class ManageViewModel : IManageViewModel
{
    private readonly CompositeDisposable disposables = new();

    public ManageViewModel(
        IFunded project,
        UIServices uiServices,
        IInvestmentAppService appService,
        IWalletContext walletContext
    )
    {
        Funded = project;

        var canCancelApproval =
            project.InvestorData.Status.Select(status => status == InvestmentStatus.PendingFounderSignatures);
        var canInvest =
            project.InvestorData.Status.Select(status => status == InvestmentStatus.FounderSignaturesReceived);
        var canCancelInvestment = canInvest;

        OpenChat = EnhancedCommand.CreateWithResult(Result.Success);

        CancelApproval = EnhancedCommand.CreateWithResult(
            () => DoCancelInvestment(project, uiServices, appService, walletContext),
            canCancelApproval).DisposeWith(disposables);
        CancelInvestment = EnhancedCommand.CreateWithResult(
            () => DoCancelInvestment(project, uiServices, appService, walletContext),
            canCancelInvestment).DisposeWith(disposables);
        ConfirmInvestment = EnhancedCommand.CreateWithResult(
            () => DoConfirmInvestment(project, uiServices, appService, walletContext),
            canInvest).DisposeWith(disposables);

        var refreshableCommands = CancelApproval.Merge(CancelInvestment).Merge(ConfirmInvestment).ToSignal();

        refreshableCommands.InvokeCommand(Funded.InvestorData.Refresh).DisposeWith(disposables);
    }

    private static async Task<Result> DoCancelInvestment(
        IFunded project,
        UIServices uiServices,
        IInvestmentAppService appService,
        IWalletContext walletContext
    )
    {
        return await walletContext.Require()
                                  .Bind(wallet =>
                                  {
                                      CancelInvestmentRequest.CancelInvestmentRequestRequest
                                          cancelInvestmentRequest = new(
                                              wallet.Id,
                                              project.Project.Id,
                                              project.InvestorData.InvestmentId);
                                      return appService.CancelInvestmentRequest(cancelInvestmentRequest);
                                  })
                                  .Tap(() => uiServices.NotificationService.Show(
                                           "The investment has been canceled",
                                           "Canceled"))
                                  .TapError(error => uiServices.NotificationService.Show(
                                                $"Failed to cancel investment: {error}",
                                                "Canceled"));
    }

    private static async Task<Result> DoConfirmInvestment(
        IFunded project,
        UIServices uiServices,
        IInvestmentAppService appService,
        IWalletContext walletContext
    )
    {
        return await walletContext.Require()
                                  .Bind(wallet =>
                                  {
                                      PublishInvestment.PublishInvestmentRequest publishInvestmentRequest = new(
                                          project.InvestorData.InvestmentId,
                                          wallet.Id,
                                          project.Project.Id);

                                      return appService.ConfirmInvestment(publishInvestmentRequest);
                                  })
                                  .Tap(() => uiServices.NotificationService.Show(
                                           "The investment has been confirmed",
                                           "Confirmed"))
                                  .TapError(error => uiServices.NotificationService.Show(
                                                $"Failed to confirm investment: {error}",
                                                "Confirmed"));
    }

    public IFunded Funded { get; }
    public IEnhancedCommand<Result> CancelApproval { get; }
    public IEnhancedCommand<Result> OpenChat { get; }
    public IEnhancedCommand<Result> CancelInvestment { get; }
    public IEnhancedCommand<Result> ConfirmInvestment { get; }
}
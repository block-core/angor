using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Flows.InvestV2.InvestmentResult;
using AngorApp.UI.Shell;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Flows.InvestV2.Invoice;

public partial class InvoiceViewModel : ReactiveObject, IInvoiceViewModel, IValidatable, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly BehaviorSubject<bool> paymentReceivedSubject = new(false);
    private ICloseable? closeable;

    [Reactive] private IEnumerable<IInvoiceType> invoiceTypes = [new InvoiceTypeSample { Name = "Loading...", Address = "" }];
    [Reactive] private IInvoiceType? selectedInvoiceType;

    public InvoiceViewModel(
        IWallet wallet, 
        IInvestmentAppService investmentAppService, 
        UIServices uiServices, 
        IAmountUI amount,
        ProjectId projectId,
        IShellViewModel shell)
    {
        Amount = amount;
        PaymentReceived = paymentReceivedSubject.AsObservable();
        
        // Create copy address command
        var canCopy = this.WhenAnyValue(x => x.SelectedInvoiceType)
            .Select(x => !string.IsNullOrEmpty(x?.Address));
        
        CopyAddress = ReactiveCommand.CreateFromTask(CopyAddressToClipboard, canCopy).Enhance();
        
        // Start initialization asynchronously on the UI thread
        Observable.StartAsync(async ct => await InitializeAsync(wallet, investmentAppService, uiServices, projectId, shell, ct), RxApp.MainThreadScheduler)
            .Subscribe()
            .DisposeWith(disposable);
    }
    
    public void SetCloseable(ICloseable closeable)
    {
        this.closeable = closeable;
    }

    private async Task CopyAddressToClipboard()
    {
        if (SelectedInvoiceType?.Address == null)
            return;
            
        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(SelectedInvoiceType.Address);
        }
    }
    
    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.Clipboard;
        }
        
        return null;
    }

    private async Task InitializeAsync(
        IWallet wallet,
        IInvestmentAppService investmentAppService,
        UIServices uiServices,
        ProjectId projectId,
        IShellViewModel shell,
        CancellationToken cancellationToken)
    {
        // Link with our disposal token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
        var token = linkedCts.Token;

        try
        {
            // Generate receive address from wallet
            var addressResult = await wallet.GenerateReceiveAddress();

            if (addressResult.IsFailure)
            {
                await uiServices.NotificationService.Show(
                    $"Failed to generate address: {addressResult.Error}",
                    "Address Generation Error");
                return;
            }

            var address = addressResult.Value;

            // Update invoice type with wallet name + "Address"
            InvoiceTypes =
            [
                new InvoiceTypeSample
                {
                    Name = $"{wallet.Name} Address",
                    Address = address
                }
            ];
            SelectedInvoiceType = InvoiceTypes.First();

            // Start monitoring the address for funds
            var monitorResult = await MonitorAddressForFundsAsync(wallet.Id, address, investmentAppService, uiServices, token);
            
            if (monitorResult.IsFailure)
            {
                return;
            }

            // Funds received - now build and submit the investment transaction
            await BuildAndSubmitInvestmentAsync(wallet.Id, projectId, address, investmentAppService, uiServices, shell, token);
        }
        catch (OperationCanceledException)
        {
            // Normal cleanup, don't show error
        }
    }

    private async Task<Result> MonitorAddressForFundsAsync(
        WalletId walletId,
        string address,
        IInvestmentAppService investmentAppService,
        UIServices uiServices,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
                walletId,
                address,
                new Amount(Amount.Sats),
                TimeSpan.FromMinutes(10));

            var result = await investmentAppService.MonitorAddressForFunds(request, cancellationToken);

            // Check if cancelled before continuing
            cancellationToken.ThrowIfCancellationRequested();

            if (result.IsFailure)
            {
                await uiServices.NotificationService.Show(
                    result.Error,
                    "Payment Monitoring Failed");
                return Result.Failure(result.Error);
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure("Monitoring cancelled");
        }
        catch (Exception ex)
        {
            await uiServices.NotificationService.Show(
                $"Error monitoring address: {ex.Message}",
                "Payment Monitoring Error");
            return Result.Failure(ex.Message);
        }
    }

    private async Task BuildAndSubmitInvestmentAsync(
        WalletId walletId,
        ProjectId projectId,
        string fundingAddress,
        IInvestmentAppService investmentAppService,
        UIServices uiServices,
        IShellViewModel shell,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build the investment draft using the funding address
            var buildRequest = new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                walletId,
                projectId,
                new Amount(Amount.Sats),
                new DomainFeerate(20), // TODO: Make fee rate configurable
                FundingAddress: fundingAddress);

            var buildResult = await investmentAppService.BuildInvestmentDraft(buildRequest);

            if (buildResult.IsFailure)
            {
                await uiServices.NotificationService.Show(
                    buildResult.Error,
                    "Investment Build Failed");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var investmentDraft = buildResult.Value.InvestmentDraft;

            // Check if the investment is above the penalty threshold
            var thresholdRequest = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(
                projectId,
                new Amount(Amount.Sats));

            var thresholdResult = await investmentAppService.IsInvestmentAbovePenaltyThreshold(thresholdRequest);

            if (thresholdResult.IsFailure)
            {
                await uiServices.NotificationService.Show(
                    thresholdResult.Error,
                    "Threshold Check Failed");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var isAboveThreshold = thresholdResult.Value.IsAboveThreshold;

            if (isAboveThreshold)
            {
                // Above threshold: Request founder signatures via SubmitInvestment
                var submitRequest = new RequestInvestmentSignatures.RequestFounderSignaturesRequest(
                    walletId,
                    projectId,
                    investmentDraft);

                var submitResult = await investmentAppService.SubmitInvestment(submitRequest);

                if (submitResult.IsFailure)
                {
                    await uiServices.NotificationService.Show(
                        submitResult.Error,
                        "Investment Submission Failed");
                    return;
                }

                // Investment submitted, waiting for founder approval
                paymentReceivedSubject.OnNext(true);
                
                // Close the invoice dialog and show investment result dialog
                closeable?.Close();
                var resultViewModel = new InvestResultViewModel(shell) { Amount = Amount };
                await uiServices.Dialog.Show(resultViewModel, Observable.Return("Investment Submitted"), (model, c) => model.Options(c));
            }
            else
            {
                // Below threshold: Directly publish the transaction (no founder approval needed)
                var publishRequest = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value,
                    projectId,
                    investmentDraft);

                var publishResult = await investmentAppService.SubmitTransactionFromDraft(publishRequest);

                if (publishResult.IsFailure)
                {
                    await uiServices.NotificationService.Show(
                        publishResult.Error,
                        "Investment Publish Failed");
                    return;
                }

                // Investment completed
                paymentReceivedSubject.OnNext(true);
                
                // Close the invoice dialog and show investment result dialog
                closeable?.Close();
                var resultViewModel = new InvestResultViewModel(shell) { Amount = Amount };
                await uiServices.Dialog.Show(resultViewModel, Observable.Return("Investment Completed"), (model, c) => model.Options(c));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cleanup, don't show error
        }
        catch (Exception ex)
        {
            await uiServices.NotificationService.Show(
                $"Error processing investment: {ex.Message}",
                "Investment Error");
        }
    }

    public IObservable<bool> PaymentReceived { get; }

    public IAmountUI Amount { get; }
    
    public IEnhancedCommand CopyAddress { get; }

    public IObservable<bool> IsValid => PaymentReceived;

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        paymentReceivedSubject.Dispose();
        disposable.Dispose();
    }
}
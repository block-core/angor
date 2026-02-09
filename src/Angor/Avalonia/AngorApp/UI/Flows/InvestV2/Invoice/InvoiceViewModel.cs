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
    
    // Separate CTS for monitoring that can be cancelled when invoice type changes
    private CancellationTokenSource? monitoringCts;
    private readonly object monitoringLock = new();

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
        
        // React to invoice type changes - cancel current monitoring and start new one
        this.WhenAnyValue(x => x.SelectedInvoiceType)
            .Where(x => x != null)
            .Skip(1) // Skip initial selection (handled in InitializeAsync)
            .SelectMany(invoiceType => 
                Observable.FromAsync(ct => MonitorAndProcessPaymentAsync(invoiceType!, wallet.Id, projectId, investmentAppService, uiServices, shell, ct)))
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

            // Create the on-chain invoice type
            var onChainInvoice = new OnChainInvoiceType
            {
                Name = $"{wallet.Name} Address",
                Address = address
            };

            // Create Lightning swap to get invoice
            var lightningRequest = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
                wallet.Id,
                projectId,
                new Amount(Amount.Sats),
                address);

            var lightningResult = await investmentAppService.CreateLightningSwap(lightningRequest);

            if (lightningResult.IsSuccess)
            {
                var swap = lightningResult.Value.Swap;
                
                // TODO: Lightning invoices expire (~10 min). Consider showing expiry countdown in UI
                // or auto-regenerating the invoice when it expires.
                
                // Add both options to InvoiceTypes
                InvoiceTypes =
                [
                    onChainInvoice,
                    new LightningInvoiceType
                    {
                        Name = "Lightning",
                        Address = swap.Invoice,
                        SwapId = swap.Id,
                        ReceivingAddress = address
                    }
                ];
            }
            else
            {
                // Lightning swap failed, only show on-chain option
                InvoiceTypes = [onChainInvoice];
            }

            SelectedInvoiceType = InvoiceTypes.First();

            // Start monitoring for the initially selected invoice type
            await MonitorAndProcessPaymentAsync(SelectedInvoiceType, wallet.Id, projectId, investmentAppService, uiServices, shell, token);
        }
        catch (OperationCanceledException)
        {
            // Normal cleanup, don't show error
        }
    }

    private async Task MonitorAndProcessPaymentAsync(
        IInvoiceType invoiceType,
        WalletId walletId,
        ProjectId projectId,
        IInvestmentAppService investmentAppService,
        UIServices uiServices,
        IShellViewModel shell,
        CancellationToken cancellationToken)
    {
        // Cancel any existing monitoring before starting new one
        CancelCurrentMonitoring();
        
        // Create new CTS for this monitoring session, linked to both the passed token and our disposal token
        CancellationToken token;
        lock (monitoringLock)
        {
            monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            token = monitoringCts.Token;
        }
        try
        {
            Result monitorResult;
            
            if (invoiceType.PaymentMethod == PaymentMethod.Lightning && invoiceType is LightningInvoiceType lightningInvoice)
            {
                monitorResult = await MonitorLightningSwapAsync(
                    walletId,
                    lightningInvoice.SwapId,
                    lightningInvoice.ReceivingAddress,
                    investmentAppService,
                    uiServices,
                    token);
            }
            else
            {
                monitorResult = await MonitorAddressForFundsAsync(
                    walletId,
                    invoiceType.ReceivingAddress ?? invoiceType.Address,
                    investmentAppService,
                    uiServices,
                    token);
            }
            
            if (monitorResult.IsFailure)
            {
                return;
            }

            // Funds received - now build and submit the investment transaction
            var receivingAddress = invoiceType.ReceivingAddress ?? invoiceType.Address;
            await BuildAndSubmitInvestmentAsync(walletId, projectId, receivingAddress, investmentAppService, uiServices, shell, token);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation when switching invoice types
        }
    }

    private void CancelCurrentMonitoring()
    {
        lock (monitoringLock)
        {
            if (monitoringCts != null)
            {
                monitoringCts.Cancel();
                monitoringCts.Dispose();
                monitoringCts = null;
            }
        }
    }

    private async Task<Result> MonitorLightningSwapAsync(
        WalletId walletId,
        string swapId,
        string receivingAddress,
        IInvestmentAppService investmentAppService,
        UIServices uiServices,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
                walletId,
                swapId,
                receivingAddress,
                TimeSpan.FromMinutes(10));

            var result = await investmentAppService.MonitorLightningSwap(request);

            // Check if cancelled before continuing
            cancellationToken.ThrowIfCancellationRequested();

            if (result.IsFailure)
            {
                await uiServices.NotificationService.Show(
                    result.Error,
                    "Lightning Payment Monitoring Failed");
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
                $"Error monitoring Lightning payment: {ex.Message}",
                "Lightning Payment Error");
            return Result.Failure(ex.Message);
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
        CancelCurrentMonitoring();
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        paymentReceivedSubject.Dispose();
        disposable.Dispose();
    }
}
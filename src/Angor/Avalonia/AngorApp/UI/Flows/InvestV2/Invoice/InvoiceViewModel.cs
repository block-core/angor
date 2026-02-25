using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Flows.InvestV2.InvestmentResult;
using AngorApp.UI.Shell;
using Avalonia.Controls;
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
    
    // Fee rate in sats/vbyte - used for both Lightning swap calculation and investment transaction
    private const int DefaultFeeRateSatsPerVbyte = 2;
    
    // Separate CTS for monitoring that can be cancelled when invoice type changes
    private CancellationTokenSource? monitoringCts;
    private readonly object monitoringLock = new();
    
    // Dependencies stored for lazy loading Lightning invoice
    private IWallet? wallet;
    private IInvestmentAppService? investmentAppService;
    private UIServices? uiServices;
    private ProjectId? projectId;
    private IShellViewModel? shell;
    private string? generatedAddress;

    [Reactive] private IEnumerable<IInvoiceType> invoiceTypes = [new InvoiceTypeSample { Name = "Loading...", Address = "" }];
    [Reactive] private IInvoiceType? selectedInvoiceType;
    [Reactive] private bool isLoadingInvoice;

    public InvoiceViewModel(
        IWallet wallet, 
        IInvestmentAppService investmentAppService, 
        UIServices uiServices, 
        IAmountUI amount,
        ProjectId projectId,
        IShellViewModel shell)
    {
        // Store dependencies for lazy loading
        this.wallet = wallet;
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;
        this.projectId = projectId;
        this.shell = shell;
        
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
        
        // React to invoice type changes - load data if needed and start monitoring
        this.WhenAnyValue(x => x.SelectedInvoiceType)
            .Where(x => x != null)
            .Skip(1) // Skip initial selection (handled in InitializeAsync)
            .SelectMany(invoiceType => 
                Observable.FromAsync(ct => OnInvoiceTypeSelectedAsync(invoiceType!, ct)))
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
            return TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
        }
        
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            return TopLevel.GetTopLevel(singleView.MainView)?.Clipboard;
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
            generatedAddress = address;

            // Create the on-chain invoice type
            var onChainInvoice = new OnChainInvoiceType
            {
                Name = $"{wallet.Name} Address",
                Address = address
            };

            // Create placeholder Lightning invoice type (will be loaded when selected)
            var lightningInvoice = new LightningInvoiceType
            {
                Name = "Lightning",
                ReceivingAddress = address
            };
            // Address and SwapId are reactive properties, set after construction
            lightningInvoice.Address = ""; // Will be populated when selected
            lightningInvoice.SwapId = null; // Will be populated when selected

            // Create placeholder Liquid invoice type (will be loaded when selected)
            var liquidInvoice = new LiquidInvoiceType
            {
                Name = "Liquid",
                ReceivingAddress = address
            };
            liquidInvoice.Address = ""; // Will be populated when selected
            liquidInvoice.SwapId = null; // Will be populated when selected

            // Add all options to InvoiceTypes (Lightning and Liquid will lazy load when selected)
            InvoiceTypes = [onChainInvoice, lightningInvoice, liquidInvoice];

            SelectedInvoiceType = onChainInvoice;

            // Start monitoring for the initially selected invoice type (on-chain)
            await MonitorAndProcessPaymentAsync(SelectedInvoiceType, wallet.Id, projectId, investmentAppService, uiServices, shell, token);
        }
        catch (OperationCanceledException)
        {
            // Normal cleanup, don't show error
        }
    }

    private async Task OnInvoiceTypeSelectedAsync(IInvoiceType invoiceType, CancellationToken cancellationToken)
    {
        if (wallet == null || investmentAppService == null || uiServices == null || projectId == null || shell == null)
            return;
            
        // Link with our disposal token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
        var token = linkedCts.Token;

        try
        {
            // If this is a Lightning invoice that hasn't been loaded yet, load it
            if (invoiceType is LightningInvoiceType lightningInvoice && !lightningInvoice.IsLoaded)
            {
                IsLoadingInvoice = true;
                
                var loadResult = await LoadLightningInvoiceAsync(lightningInvoice, token);
                
                IsLoadingInvoice = false;
                
                if (loadResult.IsFailure)
                {
                    // Loading failed, switch back to on-chain
                    await uiServices.NotificationService.Show(
                        loadResult.Error,
                        "Lightning Invoice Error");
                    SelectedInvoiceType = InvoiceTypes.FirstOrDefault(x => x.PaymentMethod == PaymentMethod.OnChain);
                    return;
                }
            }

            // If this is a Liquid invoice that hasn't been loaded yet, load it
            if (invoiceType is LiquidInvoiceType liquidInvoice && !liquidInvoice.IsLoaded)
            {
                IsLoadingInvoice = true;
                
                var loadResult = await LoadLiquidInvoiceAsync(liquidInvoice, token);
                
                IsLoadingInvoice = false;
                
                if (loadResult.IsFailure)
                {
                    // Loading failed, switch back to on-chain
                    await uiServices.NotificationService.Show(
                        loadResult.Error,
                        "Liquid Invoice Error");
                    SelectedInvoiceType = InvoiceTypes.FirstOrDefault(x => x.PaymentMethod == PaymentMethod.OnChain);
                    return;
                }
            }

            // Start monitoring for the selected invoice type
            await MonitorAndProcessPaymentAsync(invoiceType, wallet.Id, projectId, investmentAppService, uiServices, shell, token);
        }
        catch (OperationCanceledException)
        {
            // Normal cleanup
        }
    }

    private async Task<Result> LoadLightningInvoiceAsync(LightningInvoiceType lightningInvoice, CancellationToken cancellationToken)
    {
        if (wallet == null || investmentAppService == null || projectId == null || generatedAddress == null)
            return Result.Failure("Dependencies not initialized");

        try
        {
            // Create Lightning swap to get invoice
            // TODO: Lightning invoices expire (~10 min). Consider showing expiry countdown in UI
            // or auto-regenerating the invoice when it expires.
            var lightningRequest = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
                wallet.Id,
                projectId,
                new Amount(Amount.Sats),
                generatedAddress,
                DefaultFeeRateSatsPerVbyte);

            var lightningResult = await investmentAppService.CreateLightningSwap(lightningRequest);

            cancellationToken.ThrowIfCancellationRequested();

            if (lightningResult.IsFailure)
            {
                return Result.Failure(lightningResult.Error);
            }

            var swap = lightningResult.Value.Swap;
            
            // Update the Lightning invoice with the loaded data
            lightningInvoice.Address = swap.Invoice;
            lightningInvoice.SwapId = swap.Id;
            
            // Trigger UI update by re-setting the selected type
            this.RaisePropertyChanged(nameof(SelectedInvoiceType));

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure("Loading cancelled");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to load Lightning invoice: {ex.Message}");
        }
    }

    private async Task<Result> LoadLiquidInvoiceAsync(LiquidInvoiceType liquidInvoice, CancellationToken cancellationToken)
    {
        if (wallet == null || investmentAppService == null || projectId == null || generatedAddress == null)
            return Result.Failure("Dependencies not initialized");

        try
        {
            // Create Liquid swap to get address
            var liquidRequest = new CreateLiquidSwapForInvestment.CreateLiquidSwapRequest(
                wallet.Id,
                projectId,
                new Amount(Amount.Sats),
                generatedAddress,
                DefaultFeeRateSatsPerVbyte);

            var liquidResult = await investmentAppService.CreateLiquidSwap(liquidRequest);

            cancellationToken.ThrowIfCancellationRequested();

            if (liquidResult.IsFailure)
            {
                return Result.Failure(liquidResult.Error);
            }

            var swap = liquidResult.Value.Swap;
            
            // Update the Liquid invoice with the loaded data
            // For Liquid swaps, the Address is the Liquid lockup address to pay
            liquidInvoice.Address = swap.LockupAddress;
            liquidInvoice.SwapId = swap.Id;
            liquidInvoice.ExpectedLiquidAmount = swap.InvoiceAmount;
            
            // Trigger UI update by re-setting the selected type
            this.RaisePropertyChanged(nameof(SelectedInvoiceType));

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure("Loading cancelled");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to load Liquid invoice: {ex.Message}");
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
            // For Lightning payments, first monitor and claim the swap
            if (invoiceType.PaymentMethod == PaymentMethod.Lightning && invoiceType is LightningInvoiceType lightningInvoice)
            {
                // SwapId should never be null here since we check IsLoaded before monitoring
                if (string.IsNullOrEmpty(lightningInvoice.SwapId))
                {
                    return; // Lightning invoice not loaded yet
                }
                
                // Monitor the swap and claim funds
                var swapResult = await MonitorSwapAsync(
                    walletId,
                    lightningInvoice.SwapId,
                    investmentAppService,
                    uiServices,
                    token);
                
                if (swapResult.IsFailure)
                {
                    return;
                }
            }
            
            // For Liquid payments, monitor and claim the swap (uses same Boltz API)
            if (invoiceType.PaymentMethod == PaymentMethod.Liquid && invoiceType is LiquidInvoiceType liquidInvoice)
            {
                if (string.IsNullOrEmpty(liquidInvoice.SwapId))
                {
                    return; // Liquid invoice not loaded yet
                }
                
                // Monitor the swap - uses same MonitorLightningSwap since Boltz handles both
                var swapResult = await MonitorSwapAsync(
                    walletId,
                    liquidInvoice.SwapId,
                    investmentAppService,
                    uiServices,
                    token);
                
                if (swapResult.IsFailure)
                {
                    return;
                }
            }
            
            // Monitor the receiving address for funds (Lightning, Liquid, and on-chain payments)
            var receivingAddress = invoiceType.ReceivingAddress ?? invoiceType.Address;
            var monitorResult = await MonitorAddressForFundsAsync(
                walletId,
                receivingAddress,
                investmentAppService,
                uiServices,
                token);
            
            if (monitorResult.IsFailure)
            {
                return;
            }

            // Funds received - now build and submit the investment transaction
            await BuildAndSubmitInvestmentAsync(walletId, projectId, receivingAddress, DefaultFeeRateSatsPerVbyte, investmentAppService, uiServices, shell, token);
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

    private static async Task<Result> MonitorSwapAsync(
        WalletId walletId,
        string swapId,
        IInvestmentAppService investmentAppService,
        UIServices uiServices,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
                walletId,
                swapId,
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
        int feeRateSatsPerVbyte,
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
                new DomainFeerate(feeRateSatsPerVbyte),
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
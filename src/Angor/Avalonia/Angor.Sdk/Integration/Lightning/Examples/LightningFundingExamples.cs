using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Wallet.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning.Examples;

/// <summary>
/// Example usage of Lightning Network integration for funding investments.
/// This demonstrates the complete flow from creating an invoice to receiving on-chain funds.
/// </summary>
public class LightningFundingExample
{
    private readonly IInvestmentAppService _investmentAppService;
    private readonly ILogger<LightningFundingExample> _logger;

    public LightningFundingExample(
        IInvestmentAppService investmentAppService,
        ILogger<LightningFundingExample> logger)
    {
        _investmentAppService = investmentAppService;
        _logger = logger;
    }

    /// <summary>
    /// Example 1: Simple orchestrated flow - easiest way to use Lightning funding
    /// </summary>
    public async Task<bool> SimpleOrchestratedFlowExample(
        WalletId walletId, 
        string projectId, 
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Example 1: Simple Orchestrated Flow ===");

        var request = new FundInvestmentViaLightning.FundInvestmentViaLightningRequest(
            WalletId: walletId,
            ProjectId: projectId,
            InvestmentAmount: new Amount(amountSats),
            ReceivingAddress: receivingAddress,
            InvoiceTimeout: TimeSpan.FromMinutes(30)
        );

        var result = await _investmentAppService.FundInvestmentViaLightning(request);

        if (result.IsSuccess)
        {
            _logger.LogInformation("✓ Lightning funding completed successfully!");
            _logger.LogInformation("  Invoice Bolt11: {Bolt11}", result.Value.Invoice.Bolt11);
            _logger.LogInformation("  Total Received: {Amount} sats", result.Value.TotalAmountReceived.Sats);
            _logger.LogInformation("  UTXOs detected: {Count}", result.Value.DetectedUtxos.Count);
            
            // Now you can use result.Value.DetectedUtxos for the investment transaction
            return true;
        }
        else
        {
            _logger.LogError("✗ Lightning funding failed: {Error}", result.Error);
            return false;
        }
    }

    /// <summary>
    /// Example 2: Step-by-step flow - more control over each step
    /// </summary>
    public async Task<bool> StepByStepFlowExample(
        WalletId walletId,
        string projectId,
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Example 2: Step-by-Step Flow ===");

        // Step 1: Create Lightning invoice
        _logger.LogInformation("Step 1: Creating Lightning invoice...");
        
        var createInvoiceRequest = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
            WalletId: walletId,
            ProjectId: projectId,
            InvestmentAmount: new Amount(amountSats),
            Memo: $"Investment in project {projectId.Substring(0, Math.Min(8, projectId.Length))}"
        );

        var invoiceResult = await _investmentAppService.CreateLightningInvoiceForInvestment(createInvoiceRequest);

        if (invoiceResult.IsFailure)
        {
            _logger.LogError("✗ Failed to create invoice: {Error}", invoiceResult.Error);
            return false;
        }

        var invoice = invoiceResult.Value.Invoice;
        var boltWalletId = invoiceResult.Value.BoltWalletId;
        
        _logger.LogInformation("✓ Invoice created successfully!");
        _logger.LogInformation("  Invoice ID: {InvoiceId}", invoice.Id);
        _logger.LogInformation("  Bolt11: {Bolt11}", invoice.Bolt11);
        _logger.LogInformation("  Amount: {Amount} sats", invoice.AmountSats);
        _logger.LogInformation("  Expires: {ExpiresAt}", invoice.ExpiresAt);

        // At this point, display the invoice.Bolt11 to the user as a QR code
        // or provide it for copy-paste into their Lightning wallet

        // Step 2: Monitor invoice for payment and handle swap
        _logger.LogInformation("Step 2: Monitoring invoice for payment...");
        _logger.LogInformation("  Waiting for user to pay the invoice...");

        var monitorRequest = new MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceRequest(
            WalletId: walletId,
            InvoiceId: invoice.Id,
            BoltWalletId: boltWalletId,
            TargetAddress: receivingAddress,
            Timeout: TimeSpan.FromMinutes(30)
        );

        var monitorResult = await _investmentAppService.MonitorLightningInvoiceAndSwap(monitorRequest);

        if (monitorResult.IsFailure)
        {
            _logger.LogError("✗ Monitoring/swap failed: {Error}", monitorResult.Error);
            return false;
        }

        _logger.LogInformation("✓ Payment received and swapped successfully!");
        _logger.LogInformation("  Swap Address: {SwapAddress}", monitorResult.Value.SwapAddress);
        
        if (monitorResult.Value.DetectedUtxos != null && monitorResult.Value.DetectedUtxos.Any())
        {
            _logger.LogInformation("  UTXOs detected: {Count}", monitorResult.Value.DetectedUtxos.Count);
            var totalAmount = monitorResult.Value.DetectedUtxos.Sum(u => u.value);
            _logger.LogInformation("  Total on-chain: {Amount} sats", totalAmount);
            return true;
        }
        else
        {
            _logger.LogWarning("  No UTXOs detected yet - swap may still be pending");
            return false;
        }
    }

    /// <summary>
    /// Example 3: Create invoice only (for manual monitoring)
    /// </summary>
    public async Task<string?> CreateInvoiceOnlyExample(
        WalletId walletId,
        string projectId,
        long amountSats)
    {
        _logger.LogInformation("=== Example 3: Create Invoice Only ===");

        var request = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
            WalletId: walletId,
            ProjectId: projectId,
            InvestmentAmount: new Amount(amountSats),
            Memo: "Investment invoice"
        );

        var result = await _investmentAppService.CreateLightningInvoiceForInvestment(request);

        if (result.IsSuccess)
        {
            var bolt11 = result.Value.Invoice.Bolt11;
            var invoiceId = result.Value.Invoice.Id;
            
            _logger.LogInformation("✓ Invoice created!");
            _logger.LogInformation("  Invoice ID: {InvoiceId}", invoiceId);
            _logger.LogInformation("  Bolt11: {Bolt11}", bolt11);
            
            // Store invoiceId for later monitoring
            // Display bolt11 to user
            return bolt11;
        }
        else
        {
            _logger.LogError("✗ Failed to create invoice: {Error}", result.Error);
            return null;
        }
    }

    /// <summary>
    /// Complete end-to-end example with error handling
    /// </summary>
    public async Task CompleteExampleWithErrorHandling(
        WalletId walletId,
        string projectId,
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Complete End-to-End Example ===");

        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(walletId.Value))
            {
                _logger.LogError("Invalid wallet ID");
                return;
            }

            if (amountSats < 1000) // Minimum 1000 sats
            {
                _logger.LogError("Amount too small (minimum 1000 sats)");
                return;
            }

            _logger.LogInformation("Starting Lightning funding for {Amount} sats", amountSats);

            var request = new FundInvestmentViaLightning.FundInvestmentViaLightningRequest(
                WalletId: walletId,
                ProjectId: projectId,
                InvestmentAmount: new Amount(amountSats),
                ReceivingAddress: receivingAddress
            );

            _logger.LogInformation("Calling FundInvestmentViaLightning...");
            var result = await _investmentAppService.FundInvestmentViaLightning(request);

            if (result.IsSuccess)
            {
                _logger.LogInformation("========================================");
                _logger.LogInformation("SUCCESS! Lightning funding completed");
                _logger.LogInformation("========================================");
                _logger.LogInformation("Invoice Details:");
                _logger.LogInformation("  - ID: {Id}", result.Value.Invoice.Id);
                _logger.LogInformation("  - Payment Hash: {Hash}", result.Value.Invoice.PaymentHash);
                _logger.LogInformation("  - Paid At: {PaidAt}", result.Value.Invoice.PaidAt);
                _logger.LogInformation("");
                _logger.LogInformation("Swap Details:");
                _logger.LogInformation("  - Swap Address: {Address}", result.Value.SwapAddress);
                _logger.LogInformation("  - Bolt Wallet ID: {WalletId}", result.Value.BoltWalletId);
                _logger.LogInformation("");
                _logger.LogInformation("On-Chain Results:");
                _logger.LogInformation("  - Total Received: {Amount} sats", result.Value.TotalAmountReceived.Sats);
                _logger.LogInformation("  - Number of UTXOs: {Count}", result.Value.DetectedUtxos.Count);
                
                foreach (var utxo in result.Value.DetectedUtxos)
                {
                    _logger.LogInformation("    • UTXO: {TxId}:{Index} = {Value} sats",
                        utxo.outpoint.transactionId,
                        utxo.outpoint.outputIndex,
                        utxo.value);
                }

                _logger.LogInformation("");
                _logger.LogInformation("Next Steps:");
                _logger.LogInformation("  - Use the detected UTXOs to build your investment transaction");
                _logger.LogInformation("  - Call BuildInvestmentDraft with these UTXOs");
                _logger.LogInformation("========================================");
            }
            else
            {
                _logger.LogError("========================================");
                _logger.LogError("FAILED: Lightning funding unsuccessful");
                _logger.LogError("========================================");
                _logger.LogError("Error: {Error}", result.Error);
                
                // Handle specific error cases
                if (result.Error.Contains("Timeout"))
                {
                    _logger.LogError("The invoice was not paid within the timeout period.");
                    _logger.LogError("Suggestions:");
                    _logger.LogError("  - Check if the invoice was actually paid");
                    _logger.LogError("  - Verify your Lightning wallet has sufficient balance");
                    _logger.LogError("  - Try increasing the timeout period");
                }
                else if (result.Error.Contains("expired"))
                {
                    _logger.LogError("The invoice expired before being paid.");
                    _logger.LogError("Please create a new invoice.");
                }
                else if (result.Error.Contains("Failed to create wallet"))
                {
                    _logger.LogError("Could not create or access Bolt wallet.");
                    _logger.LogError("Check your BOLT_API_KEY environment variable.");
                }
                
                _logger.LogError("========================================");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Lightning funding");
            _logger.LogError("Exception Type: {Type}", ex.GetType().Name);
            _logger.LogError("Message: {Message}", ex.Message);
            
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner Exception: {InnerMessage}", ex.InnerException.Message);
            }
        }
    }
}

/// <summary>
/// Helper class to set up and run the examples
/// </summary>
public static class LightningExamplesRunner
{
    public static async Task RunAllExamples()
    {
        // Set up dependency injection
        var services = new ServiceCollection();
        
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Register Angor SDK services
        // Note: In a real application, you'd call FundingContextServices.Register(services, logger)
        
        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        
        // Get the example class
        var examples = serviceProvider.GetRequiredService<LightningFundingExample>();
        
        // Example wallet ID and project ID (replace with real values)
        var walletId = new WalletId("test-wallet-123");
        var projectId = "test-project-abc";
        var amountSats = 100000L; // 0.001 BTC
        var receivingAddress = "bc1qtest123..."; // Replace with real address
        
        Console.WriteLine("Lightning Network Integration Examples");
        Console.WriteLine("======================================\n");
        
        // Run Example 1
        await examples.SimpleOrchestratedFlowExample(walletId, projectId, amountSats, receivingAddress);
        
        Console.WriteLine("\n\n");
        
        // Run Example 2
        await examples.StepByStepFlowExample(walletId, projectId, amountSats, receivingAddress);
        
        Console.WriteLine("\n\n");
        
        // Run Example 3
        await examples.CreateInvoiceOnlyExample(walletId, projectId, amountSats);
        
        Console.WriteLine("\n\n");
        
        // Run Complete Example
        await examples.CompleteExampleWithErrorHandling(walletId, projectId, amountSats, receivingAddress);
    }
}


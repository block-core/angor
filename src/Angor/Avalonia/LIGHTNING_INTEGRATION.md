# Lightning Network Integration for Angor SDK

This integration adds support for funding investments via the Lightning Network using the Bolt API. Users can send Lightning payments that are automatically converted to on-chain Bitcoin and used to fund investment transactions.

## Overview

The Lightning integration provides three main operations:

1. **Create Lightning Invoice** - Generate a Lightning invoice for an investment amount
2. **Monitor & Swap** - Monitor invoice payment and swap to on-chain Bitcoin
3. **Full Flow Orchestration** - Complete end-to-end flow from Lightning payment to investment

## Architecture

### Components

- **IBoltService** - Interface for Bolt Lightning API operations
- **BoltService** - Implementation of Bolt API client
- **BoltModels** - Data models for Lightning wallets, invoices, and payments
- **Lightning Operations**:
  - `CreateLightningInvoiceForInvestment` - Creates invoices
  - `MonitorLightningInvoiceAndSwap` - Monitors payments and handles swaps
  - `FundInvestmentViaLightning` - Orchestrates the complete flow

### Flow Diagram

```
User wants to invest
    ↓
1. Create Lightning Invoice
    ↓
2. User pays invoice (external Lightning wallet)
    ↓
3. Monitor invoice status (polling)
    ↓
4. When paid, request swap address from Bolt
    ↓
5. Monitor swap address for on-chain transaction
    ↓
6. Detect UTXOs on Angor wallet address
    ↓
7. Use UTXOs for investment transaction (existing flow)
```

## Configuration

### Environment Variables

Set the following environment variables:

```bash
BOLT_API_KEY=your_bolt_api_key_here
BOLT_API_URL=https://api.bolt.observer  # Optional, defaults to this
```

### Service Registration

The Lightning services are automatically registered in `FundingContextServices.cs`:

```csharp
services.TryAddSingleton<BoltConfiguration>(...);
services.TryAddSingleton<IBoltService, BoltService>();
```

## Usage Examples

### Example 1: Simple Orchestrated Flow

The easiest way to use Lightning funding:

```csharp
var investmentAppService = serviceProvider.GetRequiredService<IInvestmentAppService>();

var request = new FundInvestmentViaLightning.FundInvestmentViaLightningRequest(
    WalletId: new WalletId("my-wallet-id"),
    ProjectId: "project-identifier",
    InvestmentAmount: new Amount(1000000), // 0.01 BTC
    ReceivingAddress: "bc1q..." // Your Angor wallet address
);

var result = await investmentAppService.FundInvestmentViaLightning(request);

if (result.IsSuccess)
{
    Console.WriteLine($"Invoice Bolt11: {result.Value.Invoice.Bolt11}");
    Console.WriteLine($"Total Received: {result.Value.TotalAmountReceived.Sats} sats");
    
    // Now use result.Value.DetectedUtxos for the investment transaction
}
```

### Example 2: Manual Step-by-Step Flow

For more control over each step:

```csharp
// Step 1: Create invoice
var createInvoiceRequest = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
    WalletId: new WalletId("my-wallet-id"),
    ProjectId: "project-id",
    InvestmentAmount: new Amount(1000000),
    Memo: "Investment in awesome project"
);

var invoiceResult = await investmentAppService.CreateLightningInvoiceForInvestment(createInvoiceRequest);

if (invoiceResult.IsSuccess)
{
    var invoice = invoiceResult.Value.Invoice;
    
    // Display invoice.Bolt11 to user (QR code, copy-paste, etc.)
    Console.WriteLine($"Pay this invoice: {invoice.Bolt11}");
    
    // Step 2: Monitor and swap
    var monitorRequest = new MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceRequest(
        WalletId: new WalletId("my-wallet-id"),
        InvoiceId: invoice.Id,
        BoltWalletId: invoiceResult.Value.BoltWalletId,
        TargetAddress: "bc1q..." // Your receiving address
    );
    
    var monitorResult = await investmentAppService.MonitorLightningInvoiceAndSwap(monitorRequest);
    
    if (monitorResult.IsSuccess && monitorResult.Value.DetectedUtxos != null)
    {
        Console.WriteLine($"Swap completed! UTXOs: {monitorResult.Value.DetectedUtxos.Count}");
        // Proceed with investment transaction
    }
}
```

### Example 3: Just Create Invoice (Manual Monitoring)

If you want to handle monitoring yourself:

```csharp
var request = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
    WalletId: new WalletId("my-wallet-id"),
    ProjectId: "project-id",
    InvestmentAmount: new Amount(500000)
);

var result = await investmentAppService.CreateLightningInvoiceForInvestment(request);

if (result.IsSuccess)
{
    var bolt11 = result.Value.Invoice.Bolt11;
    var invoiceId = result.Value.Invoice.Id;
    
    // Display to user, store invoiceId, monitor separately...
}
```

## Integration with Existing Investment Flow

Once Lightning funds are swapped to on-chain, the detected UTXOs are automatically added to the wallet's UTXO set. You can then use your existing investment transaction building flow:

```csharp
// After Lightning funding completes
var utxos = lightningResult.Value.DetectedUtxos;

// Use these UTXOs to build the investment transaction
var buildDraftRequest = new BuildInvestmentDraft.BuildInvestmentDraftRequest(
    WalletId: walletId,
    ProjectInfo: projectInfo,
    InvestmentAmount: amount,
    // ... other parameters
);

var draftResult = await investmentAppService.BuildInvestmentDraft(buildDraftRequest);
// Continue with normal investment flow...
```

## Error Handling

All operations return `Result<T>` from CSharpFunctionalExtensions:

```csharp
var result = await investmentAppService.FundInvestmentViaLightning(request);

if (result.IsFailure)
{
    Console.WriteLine($"Error: {result.Error}");
    // Handle specific errors:
    // - "Timeout waiting for Lightning invoice payment"
    // - "Invoice expired"
    // - "Failed to create wallet: 401"
    // etc.
}
else
{
    // Success!
    var response = result.Value;
}
```

## Timeouts

- **Invoice Payment Timeout**: Default 30 minutes (configurable)
- **Swap Completion Timeout**: 15 minutes (hardcoded)
- **API Request Timeout**: 30 seconds (configurable in BoltConfiguration)

## Bolt API Endpoints

The integration uses the following Bolt API endpoints:

- `POST /v1/wallets` - Create wallet
- `GET /v1/wallets/{id}` - Get wallet details
- `POST /v1/wallets/{id}/invoices` - Create invoice
- `GET /v1/invoices/{id}` - Get invoice status
- `POST /v1/wallets/{id}/swap` - Get swap address
- `POST /v1/wallets/{id}/payments` - Pay invoice
- `GET /v1/wallets/{id}/invoices` - List invoices

## Testing

Unit tests should mock `IBoltService` and `IMempoolMonitoringService`:

```csharp
var mockBoltService = new Mock<IBoltService>();
mockBoltService
    .Setup(x => x.CreateInvoiceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
    .ReturnsAsync(Result.Success(new BoltInvoice { ... }));

// Inject mock into handler for testing
```

## Security Considerations

1. **API Key Storage**: Store Bolt API keys securely (environment variables, secrets manager)
2. **Wallet Mapping**: Consider encrypting the mapping between Angor WalletId and Bolt WalletId
3. **Amount Verification**: Always verify received amounts match expected amounts
4. **Swap Confirmation**: Wait for on-chain confirmations before proceeding with investment

## Future Enhancements

- [ ] Webhook support for instant payment notifications (instead of polling)
- [ ] Support for multiple Lightning providers (LND, CLN, etc.)
- [ ] Automatic fee estimation for swaps
- [ ] Persistent storage for Lightning wallet mappings
- [ ] Support for Lightning addresses (LNURL)
- [ ] Refund flow for failed swaps

## Troubleshooting

### Invoice not being detected as paid

- Check that `BOLT_API_KEY` is correctly set
- Verify the invoice hasn't expired (default 1 hour)
- Check Bolt API logs for payment status

### Swap not completing

- Verify swap address is correct
- Check mempool for pending swap transaction
- Ensure sufficient Lightning balance exists
- May take up to 15 minutes for swap completion

### UTXOs not appearing in wallet

- Verify `ReceivingAddress` belongs to the Angor wallet
- Check that wallet account info is being refreshed
- Manually refresh wallet balance after swap

## Support

For Bolt API issues: https://docs.bolt.observer
For Angor SDK issues: [Your support channel]


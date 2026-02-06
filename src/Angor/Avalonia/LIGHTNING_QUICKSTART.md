# Lightning Integration Quick Start Guide

## 🚀 Quick Start

### 1. Set up environment variables
```bash
# Windows PowerShell
$env:BOLT_API_KEY="your_bolt_api_key"
$env:BOLT_API_URL="https://api.bolt.observer"  # Optional

# Linux/Mac
export BOLT_API_KEY="your_bolt_api_key"
export BOLT_API_URL="https://api.bolt.observer"  # Optional
```

### 2. Use in your code

```csharp
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;

// Get the service
var investmentAppService = serviceProvider.GetRequiredService<IInvestmentAppService>();

// Option A: Simple one-call approach
var result = await investmentAppService.FundInvestmentViaLightning(
    new FundInvestmentViaLightning.FundInvestmentViaLightningRequest(
        WalletId: new WalletId("my-wallet"),
        ProjectId: "project-123",
        InvestmentAmount: new Amount(100000), // sats
        ReceivingAddress: "bc1q..."
    ));

if (result.IsSuccess)
{
    Console.WriteLine($"Invoice: {result.Value.Invoice.Bolt11}");
    Console.WriteLine($"Received: {result.Value.TotalAmountReceived.Sats} sats");
    // Use result.Value.DetectedUtxos for investment transaction
}
```

## 📁 Files Created

### Core Services
- `Integration/Lightning/IBoltService.cs` - Service interface
- `Integration/Lightning/BoltService.cs` - Implementation
- `Integration/Lightning/Models/BoltModels.cs` - Data models

### Operations
- `Funding/Investor/Operations/CreateLightningInvoiceForInvestment.cs`
- `Funding/Investor/Operations/MonitorLightningInvoiceAndSwap.cs`
- `Funding/Investor/Operations/FundInvestmentViaLightning.cs`

### Documentation
- `LIGHTNING_INTEGRATION.md` - Full user guide
- `LIGHTNING_INTEGRATION_SUMMARY.md` - Implementation details
- `Integration/Lightning/Examples/LightningFundingExamples.cs` - Code examples

### Tests
- `Angor.Sdk.Tests/Integration/Lightning/LightningIntegrationTests.cs`

## 🔧 API Methods

### IInvestmentAppService new methods:
1. **CreateLightningInvoiceForInvestment** - Create invoice only
2. **MonitorLightningInvoiceAndSwap** - Monitor & swap
3. **FundInvestmentViaLightning** - Complete flow ⭐ Recommended

## 📊 Flow

```
User → Create Invoice → Display Bolt11 → User Pays
                                              ↓
                                       Invoice Paid
                                              ↓
                                       Request Swap
                                              ↓
                                       Monitor On-Chain
                                              ↓
                                       UTXOs Detected
                                              ↓
                                       Ready for Investment
```

## ⚙️ Configuration

Default settings:
- Invoice timeout: 30 minutes
- Swap timeout: 15 minutes
- Poll interval: 2 seconds
- HTTP timeout: 30 seconds

Override timeout:
```csharp
var request = new FundInvestmentViaLightning.FundInvestmentViaLightningRequest(
    // ...
    InvoiceTimeout: TimeSpan.FromMinutes(60) // 1 hour
);
```

## 🧪 Testing

Run unit tests:
```bash
dotnet test Angor.Sdk.Tests --filter "FullyQualifiedName~Lightning"
```

## 📖 Full Documentation

See `LIGHTNING_INTEGRATION.md` for:
- Detailed examples
- Error handling
- Security considerations
- Troubleshooting guide
- Future enhancements

## 🔑 Key Points

✅ **Auto-wallet creation** - Bolt wallet created automatically  
✅ **Polling-based** - No webhook setup needed (for now)  
✅ **Functional errors** - Returns `Result<T>` (no exceptions)  
✅ **Fully async** - Non-blocking operations  
✅ **Comprehensive logging** - Debug-friendly  
✅ **Testable** - Mock-based unit tests  

## 🛠️ Next Steps

1. Get Bolt API key from https://bolt.observer
2. Set environment variable
3. Use `FundInvestmentViaLightning` method
4. Display invoice to user
5. Monitor completion
6. Build investment transaction with received UTXOs

## 📝 Notes

- Lightning funds are automatically swapped to on-chain Bitcoin
- UTXOs are added to wallet balance automatically
- Compatible with existing investment transaction flow
- No changes needed to existing code

## 🆘 Support

Issues? Check troubleshooting in `LIGHTNING_INTEGRATION.md` or contact support.


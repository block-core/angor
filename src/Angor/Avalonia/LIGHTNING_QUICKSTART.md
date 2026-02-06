# Boltz Lightning Integration - Quick Start

## 🚀 Usage (2 Steps)

### Step 1: Create Swap

```csharp
var result = await investmentAppService.CreateLightningSwap(
    new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
        WalletId: new WalletId("wallet-id"),
        ProjectId: "project-id",
        InvestmentAmount: new Amount(100000),  // sats
        ReceivingAddress: "bc1q..."            // on-chain address
    ));

// Display invoice to user
string invoice = result.Value.Swap.Invoice;
string swapId = result.Value.Swap.Id;
```

### Step 2: Monitor & Complete

```csharp
var monitor = await investmentAppService.MonitorLightningSwap(
    new MonitorLightningSwap.MonitorLightningSwapRequest(
        WalletId: walletId,
        SwapId: swapId,
        ReceivingAddress: "bc1q..."
    ));

// Use UTXOs for investment
var utxos = monitor.Value.DetectedUtxos;
```

## ⚡ Flow

```
User Lightning Wallet → Pay Invoice → Boltz → On-chain TX → Investment Address
                                       ↓
                              (No custody!)
```

## 📁 Files

- `IBoltzSwapService.cs` - Interface
- `BoltzSwapService.cs` - Implementation  
- `BoltzModels.cs` - Data models
- `CreateLightningSwapForInvestment.cs` - Create swap handler
- `MonitorLightningSwap.cs` - Monitor handler

## ⚙️ Config (Optional)

```bash
BOLTZ_USE_TESTNET=true  # Use testnet
```

## 💰 Fees

- ~0.5% + miner fee (~500 sats)
- Min: ~10,000 sats
- Max: ~10,000,000 sats

## ✅ Benefits

- **No custody** - Direct swap via HTLCs
- **No API key** - Permissionless
- **Trustless** - Cryptographic guarantees
- **Simple** - 2-step flow

## 📖 Full Docs

See `LIGHTNING_INTEGRATION.md` for complete documentation.


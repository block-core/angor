# Boltz Lightning Integration for Angor SDK

This integration adds support for funding investments via Lightning Network using **Boltz submarine swaps**. Users pay a Lightning invoice, and funds go **directly on-chain** to the investment address - no intermediate wallet or custody required.

## Overview

### How It Works

```
User's Lightning Wallet → Pays Invoice → Boltz → On-chain Bitcoin → Investment Address
```

**No intermediate custody.** Boltz acts as a trustless swap provider using HTLCs (Hash Time-Locked Contracts).

## Quick Start

### 1. Create a swap

```csharp
var result = await investmentAppService.CreateLightningSwap(
    new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
        WalletId: new WalletId("my-wallet"),
        ProjectId: "project-123",
        InvestmentAmount: new Amount(100000), // sats
        ReceivingAddress: "bc1q..."           // Your investment address
    ));

if (result.IsSuccess)
{
    var invoice = result.Value.Swap.Invoice;  // Display this to user
    var swapId = result.Value.Swap.Id;        // Save for monitoring
    var fees = result.Value.PairInfo;         // Show fee breakdown
}
```

### 2. Display invoice to user

Show the `invoice` (BOLT11) as:
- QR code
- Copy-to-clipboard text
- Payment link

### 3. Monitor for completion

```csharp
var monitorResult = await investmentAppService.MonitorLightningSwap(
    new MonitorLightningSwap.MonitorLightningSwapRequest(
        WalletId: walletId,
        SwapId: swapId,
        ReceivingAddress: "bc1q..."
    ));

if (monitorResult.IsSuccess)
{
    var txId = monitorResult.Value.TransactionId;
    var utxos = monitorResult.Value.DetectedUtxos;
    // Use UTXOs for investment transaction
}
```

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                      Angor SDK                               │
├─────────────────────────────────────────────────────────────┤
│  IInvestmentAppService                                       │
│    ├── CreateLightningSwap()                                │
│    └── MonitorLightningSwap()                               │
├─────────────────────────────────────────────────────────────┤
│  IBoltzSwapService                                          │
│    ├── CreateSubmarineSwapAsync()                           │
│    ├── GetSwapStatusAsync()                                 │
│    └── GetPairInfoAsync()                                   │
├─────────────────────────────────────────────────────────────┤
│  BoltzSwapService (HTTP Client → api.boltz.exchange)        │
└─────────────────────────────────────────────────────────────┘
```

### Files

| File | Description |
|------|-------------|
| `IBoltzSwapService.cs` | Service interface for Boltz API |
| `BoltzSwapService.cs` | HTTP implementation |
| `BoltzModels.cs` | Data models (swap, status, fees) |
| `CreateLightningSwapForInvestment.cs` | MediatR handler for creating swaps |
| `MonitorLightningSwap.cs` | MediatR handler for monitoring |

## Configuration

### Environment Variables (Optional)

```bash
# Use testnet (default: false)
BOLTZ_USE_TESTNET=true

# Custom API URLs (optional)
BOLTZ_API_URL=https://api.boltz.exchange
BOLTZ_TESTNET_URL=https://testnet.boltz.exchange/api
```

### Default Settings

- **Mainnet**: `https://api.boltz.exchange`
- **Testnet**: `https://testnet.boltz.exchange/api`
- **Timeout**: 30 seconds per API call
- **Monitor timeout**: 30 minutes

## Fees

Boltz charges:
- **Percentage fee**: ~0.5% (varies)
- **Miner fee**: ~500-2000 sats (depends on mempool)

Get current fees:

```csharp
var pairInfo = await boltzSwapService.GetPairInfoAsync();
Console.WriteLine($"Fee: {pairInfo.Value.FeePercentage}%");
Console.WriteLine($"Miner fee: {pairInfo.Value.MinerFee} sats");
Console.WriteLine($"Min: {pairInfo.Value.MinAmount} sats");
Console.WriteLine($"Max: {pairInfo.Value.MaxAmount} sats");
```

## Swap States

| State | Description |
|-------|-------------|
| `Created` | Swap created, waiting for payment |
| `InvoicePaid` | Lightning invoice paid, processing |
| `TransactionMempool` | On-chain tx in mempool |
| `TransactionConfirmed` | On-chain tx confirmed |
| `TransactionClaimed` | **Complete** - funds available |
| `SwapExpired` | Timeout - no payment received |
| `InvoiceExpired` | Invoice expired |
| `TransactionRefunded` | Swap refunded |

## Error Handling

```csharp
var result = await investmentAppService.CreateLightningSwap(request);

if (result.IsFailure)
{
    switch (result.Error)
    {
        case var e when e.Contains("too small"):
            // Amount below minimum
            break;
        case var e when e.Contains("too large"):
            // Amount above maximum
            break;
        case var e when e.Contains("expired"):
            // Invoice/swap expired
            break;
        default:
            // Other error
            break;
    }
}
```

## Refunds

If a swap fails after payment, users can claim a refund using the `refundPublicKey` provided during swap creation. The SDK automatically generates this key from the wallet.

Refund timelock: ~288 blocks (~2 days)

## Comparison: Old vs New

| Aspect | Old (Bolt) | New (Boltz) |
|--------|-----------|-------------|
| Custody | Intermediate wallet | **None** |
| Flow | 3+ steps | **2 steps** |
| Trust | Custodial service | **Trustless HTLCs** |
| API Key | Required | **Not required** |
| Refunds | Manual | **Automatic timelock** |

## Testing

### Unit Tests

```bash
dotnet test --filter "FullyQualifiedName~BoltzSwap"
```

### Manual Testing

1. Set `BOLTZ_USE_TESTNET=true`
2. Get testnet Lightning wallet (e.g., Phoenix on testnet)
3. Create swap with testnet address
4. Pay invoice
5. Monitor for completion

## API Reference

### Boltz Endpoints Used

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/getpairs` | GET | Get swap limits and fees |
| `/createswap` | POST | Create submarine swap |
| `/swapstatus` | GET | Get swap status |

### Full API Docs

https://docs.boltz.exchange/

## Troubleshooting

### "Amount too small"
Boltz has minimum swap amounts (~10,000 sats). Check `GetPairInfoAsync()` for current limits.

### "Swap expired"
Invoice wasn't paid within timeout. Create a new swap.

### "Transaction not detected"
- Wait for on-chain confirmation
- Check receiving address is correct
- Verify swap status shows `TransactionClaimed`

### Refund needed
If you paid but swap failed, use the refund script with your `refundPublicKey` after the timelock expires.

## Security

- **No API keys**: Boltz is permissionless
- **HTLCs**: Cryptographic guarantees for atomic swaps
- **Refund keys**: Generated from wallet for recovery
- **Timelocks**: Funds are never stuck permanently

## Future Improvements

- [ ] WebSocket for real-time status updates
- [ ] Automatic refund claiming
- [ ] Reverse swaps (on-chain → Lightning)
- [ ] Multi-path payments for larger amounts


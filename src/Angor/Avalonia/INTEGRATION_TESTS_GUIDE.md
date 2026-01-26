# Integration Tests Guide for CreateInvestmentFromSpecificAddress

## Overview

This guide explains how to run the integration tests for the `CreateInvestmentFromSpecificAddressHandler`. These tests publish real transactions to the Bitcoin testnet and verify they are correctly spent to investments.

## Prerequisites

### 1. Testnet Bitcoin
- You need **testnet Bitcoin** (tBTC) to run these tests
- Get free testnet BTC from: https://testnet-faucet.mempool.co/
- Recommended amount: 0.01 tBTC (1,000,000 sats) per test

### 2. Network Access
- The tests require internet access to connect to:
  - Bitcoin testnet network
  - Mempool.space testnet API (https://mempool.space/testnet/api)

### 3. Test Wallet
- Tests use a standard BIP39 mnemonic (for testnet only!)
- Default: `"abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"`
- **⚠️ WARNING: NEVER use this wallet on mainnet!**

## Integration Tests

### Test 1: CreateAndPublishInvestment_WithRealTestnetFunds_PublishesSuccessfully

**Purpose**: Creates an investment transaction from a specific funding address and publishes it to testnet.

**What it tests**:
1. Handler creates a signed transaction
2. Transaction can be published to testnet
3. Transaction appears in mempool
4. All outputs are correctly formed

**How to run**:
```powershell
# Remove the Skip attribute from the test first, then:
cd C:\Users\david\RiderProjects\angor\src\Angor\Avalonia\Angor.Sdk.Tests
dotnet test --filter "FullyQualifiedName~CreateAndPublishInvestment_WithRealTestnetFunds_PublishesSuccessfully"
```

**Expected output**:
```
✅ Transaction published successfully!
Transaction ID: <txid>
Explorer URL: https://mempool.space/testnet/tx/<txid>
Funding Address: tb1q...
Investment Amount: X sats
Miner Fee: Y sats
Total Fee: Z sats
```

**Manual verification**:
1. Click the Explorer URL in the test output
2. Verify the transaction is visible on mempool.space
3. Check that inputs come from the funding address
4. Verify outputs include investment outputs (taproot or OP_RETURN)
5. Confirm fees are reasonable (typically < 10,000 sats)

### Test 2: VerifyInvestmentTransaction_ChecksOutputsCorrectly

**Purpose**: Manual verification checklist for a published transaction.

**What it verifies**:
1. Transaction has correct inputs
2. Investment outputs are properly formed
3. Fees are reasonable
4. Transaction confirms in a block

**How to run**:
```powershell
# 1. Replace "YOUR_TRANSACTION_ID_HERE" with actual txid from Test 1
# 2. Remove Skip attribute
# 3. Run:
dotnet test --filter "FullyQualifiedName~VerifyInvestmentTransaction_ChecksOutputsCorrectly"
```

### Test 3: EndToEnd_CreateMonitorAndPublish_WorksWithRealMempool

**Purpose**: Documents the full end-to-end flow with real mempool monitoring.

**What it does**:
1. Generates a funding address
2. Displays the address for you to send testnet BTC
3. Monitors mempool for incoming funds (when implemented)
4. Creates and publishes investment transaction
5. Verifies on blockchain

**How to run**:
```powershell
# This test is currently a placeholder
# To implement, you need to:
# 1. Send testnet BTC to the displayed address
# 2. Implement real mempool monitoring
# 3. Run the test
dotnet test --filter "FullyQualifiedName~EndToEnd_CreateMonitorAndPublish_WorksWithRealMempool"
```

## Running All Integration Tests

```powershell
cd C:\Users\david\RiderProjects\angor\src\Angor\Avalonia\Angor.Sdk.Tests

# Run all integration tests (they will be skipped by default)
dotnet test --filter "Category=Integration"

# To run a specific test, remove the [Skip] attribute first, then:
dotnet test --filter "FullyQualifiedName~<TestName>"
```

## Test Configuration

### Changing the Test Wallet

Edit `CreateInvestmentFromSpecificAddressIntegrationTests.cs`:

```csharp
private const string TestWalletWords = "your twelve word mnemonic here";
private const string TestWalletPassphrase = ""; // Optional passphrase
```

### Changing Network (Advanced)

To test on signet or regtest:

```csharp
// In constructor:
networkConfiguration.SetNetwork(Networks.Bitcoin.Signet()); // or Regtest()
```

## Troubleshooting

### "Failed to publish transaction"

**Possible causes**:
1. Insufficient testnet BTC in funding address
2. Network connectivity issues
3. Transaction already in mempool
4. Invalid transaction structure

**Solutions**:
- Check funding address has sufficient balance
- Verify network connectivity
- Check mempool.space testnet API is accessible
- Review transaction hex for errors

### "Transaction not found in mempool"

**Possible causes**:
1. Transaction not yet propagated
2. Transaction rejected by nodes
3. Double-spend detected

**Solutions**:
- Wait 30-60 seconds and check again
- Verify transaction on mempool.space manually
- Check for conflicting transactions

### "Address validation errors"

**Known issue**: The handler has network validation issues with testnet addresses.
- See: `CreateInvestmentFromSpecificAddressTests.cs` (unit test is skipped)
- Bug tracked in: `CreateInvestmentFromSpecificAddress.cs` line 380

## Success Criteria

A successful integration test run should show:

✅ Transaction created with valid signature
✅ Transaction published to testnet
✅ Transaction visible on block explorer
✅ Transaction has correct inputs from funding address
✅ Transaction has investment outputs (taproot)
✅ Fees are reasonable (< 10,000 sats typically)
✅ Transaction confirms in next few blocks

## Cleanup

After testing:

1. The testnet BTC will be spent in investment transactions
2. No cleanup needed - testnet coins have no value
3. Test transactions will remain on testnet blockchain forever (for audit trail)

## Next Steps

1. **Fix address validation bug** in `WalletOperations.AddInputsFromAddressAndSignTransaction`
2. **Implement real mempool monitoring** for end-to-end test
3. **Add transaction confirmation verification**
4. **Test with multiple funding addresses**
5. **Test with different project types** (Fund, Subscribe)

## Additional Resources

- Testnet Explorer: https://mempool.space/testnet
- Testnet Faucet: https://testnet-faucet.mempool.co/
- Testnet Block Explorer: https://blockstream.info/testnet/
- Bitcoin Testnet Docs: https://en.bitcoin.it/wiki/Testnet

## Notes

- These tests are **resource-intensive** (require real network calls)
- Tests are **time-consuming** (wait for mempool propagation)
- Tests are **not deterministic** (network conditions vary)
- Tests should be run **manually** before releases
- Tests are **skipped by default** in CI/CD


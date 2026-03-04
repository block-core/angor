# Plan: Add BoltzLiquidSwapIntegrationTests

Create a new integration test file for the Liquid→BTC swap flow, mirroring the structure of `BoltzSwapIntegrationTests` but testing the Liquid-specific API methods and flow.

## Steps

### 1. Create new test file
Create `BoltzLiquidSwapIntegrationTests.cs` in `Angor.Sdk.Tests/Integration/Lightning/` directory (same location as `LightningIntegrationTests.cs`)

### 2. Set up the test class with the same dependencies as `BoltzSwapIntegrationTests`:
- `BoltzSwapService`, `BoltzClaimService`, `BoltzWebSocketClient`, `BoltzSwapStorageService`
- `WalletOperations`, `DerivationOperations`, `IndexerService`
- Use the same `InMemoryBoltzSwapCollection` (reference from existing test file)

### 3. Implement test methods mirroring the Lightning tests:

| Test Method | Description |
|-------------|-------------|
| `FullLiquidSwapFlow_CreatePayAndMonitor_Success` | Full end-to-end test for Liquid→BTC swap |
| `CreateLiquidSwap_WithValidData_ReturnsSwapDetails` | Test `CreateLiquidToBtcSwapAsync` API |
| `GetLiquidSwapFees_ReturnsValidFees` | Test `GetLiquidToBtcSwapFeesAsync` |
| `CalculateLiquidAmount_ReturnsCorrectAmount` | Test `CalculateLiquidAmountAsync` |

### 4. Adapt the flow for Liquid specifics:
- No Lightning invoice - instead use `swap.LockupAddress` (Liquid address to pay)
- Include `BlindingKey` validation (important for Liquid confidential transactions)
- Modify action prompts to instruct paying L-BTC instead of Lightning invoice
- Swap is created via `CreateLiquidToBtcSwapAsync` instead of `CreateSubmarineSwapAsync`

### 5. Add appropriate attributes:
```csharp
[Trait("Category", "Integration")]
[Trait("Service", "Boltz-Liquid")]
```

## Further Considerations

1. **Test Environment**: The tests should use the same Boltz API URL (`https://boltz.thedude.cloud/`) as the Lightning tests - the same endpoint supports both Lightning and Liquid swaps.

2. **Payment Simulation**: The full flow test requires manually sending L-BTC to a Liquid address. Document how to get testnet L-BTC:
   - Use Liquid testnet faucet
   - Or use a Liquid wallet with testnet funds

3. **Key Differences from Lightning Flow**:
   - `swap.Invoice` will be empty for Liquid swaps
   - `swap.LockupAddress` contains the Liquid address to pay
   - `swap.BlindingKey` is required for Liquid confidential transactions
   - The claim process uses the same `BoltzClaimService` but may have different transaction structure

4. **Claim Process**: Verify if the existing `ClaimLightningSwap` handler can be reused or if a separate `ClaimLiquidSwap` handler is needed.


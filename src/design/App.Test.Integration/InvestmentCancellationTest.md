# InvestmentCancellationTest

## Purpose

Full end-to-end integration test for the **investment cancellation** flow. CancelInvestmentRequest is implemented in the SDK and wired in the design app (PortfolioViewModel.CancelInvestmentAsync), but had zero E2E coverage. This test validates the complete cancel + fund-release + re-invest cycle across three distinct scenarios:

1. **Cancel before founder approval** (Step 1 → Cancelled)
2. **Cancel after founder approval** (Step 2 → Cancelled)
3. **Confirm after founder approval** (Step 2 → Step 3, active investment)

## Profiles

- Founder profile: `InvestmentCancellation-Founder`
- Investor profile: `InvestmentCancellation-Investor`

All profiles are created under the application's profile directory.

## Scenario (8 Phases)

### Phase 1 — Founder setup
Founder creates a wallet, funds it via the signet faucet, and deploys a **Fund** project with 0.01 BTC approval threshold.

### Phase 2 — Cancel before approval
Investor creates a wallet, funds it, invests 0.02 BTC (above threshold → pending approval at Step 1), then cancels the pending investment before the founder approves. Verifies status = Cancelled and funds released.

### Phase 3 — Re-invest after cancel
Investor re-invests in the same project (new pending investment at Step 1).

### Phase 4 — Founder approves
Founder approves the pending investment request via the Funders section.

### Phase 5 — Cancel after approval
Investor cancels the approved investment (Step 2). Verifies status = Cancelled and funds released.

### Phase 6 — Re-invest again
Investor re-invests in the same project again.

### Phase 7 — Founder approves again
Founder approves the new pending investment request.

### Phase 8 — Investor confirms
Investor confirms the approved investment. Verifies investment reaches Step 3 (active).

## Key Validations

- Above-threshold investment stays at Step 1 (pending) until founder approval.
- `CancelInvestmentAsync` successfully cancels a pending investment (Step 1).
- `CancelInvestmentAsync` successfully cancels an approved investment (Step 2).
- Investment status transitions to "Cancelled" after cancellation.
- Investor balance is restored after cancellation (funds released).
- Re-investment after cancellation succeeds, proving no state corruption.
- Founder approval flow works end-to-end via `ApprovePendingInvestmentAsync`.
- Investor confirmation flow works end-to-end via `ConfirmApprovedInvestmentAsync`.
- The full 3-step lifecycle (invest → approve → confirm) completes successfully.

## Notes

- The test reuses the real headless Avalonia app and switches profiles by rebuilding DI per profile.
- Multiple `WithProfileWindow` calls are used to switch between Founder and Investor profiles across phases.
- Above-threshold investments (0.02 BTC > 0.01 threshold) require founder approval, keeping them at Step 1 until approved.
- The founder approval pattern uses `FundersViewModel.LoadInvestmentRequestsAsync()`, `SetFilter("waiting")`, and `ApproveSignature(id)`.
- IndexerLagTimeout (5 minutes) is used for polling because signet mempool propagation can be slow.
- The test may take 3-10 minutes depending on network conditions.

## Run

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~InvestmentCancellationTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~InvestmentCancellationTest" --logger "console;verbosity=detailed"
```

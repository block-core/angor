# InvestmentCancellationTest

## Purpose

Full end-to-end integration test for the **investment cancellation** flow. CancelInvestmentRequest is implemented in the SDK and wired in the design app (PortfolioViewModel.CancelInvestmentAsync), but had zero E2E coverage. This test validates the complete cancel + fund-release + re-invest cycle.

## Profiles

- Founder profile: `InvestCancel-Founder`
- Investor profile: `InvestCancel-Investor`

All profiles are created under the application's profile directory.

## Scenario

1. Founder profile creates a wallet, funds it via the signet faucet, and deploys a **Fund** project with a high approval threshold (`0.001 BTC`) so that investments above threshold require founder approval.
2. Investor profile creates a wallet, funds it, and invests in the project with an amount **above** the approval threshold (`0.01 BTC`).
3. The investment stays at Step 1 (pending founder approval), which makes it cancellable.
4. Investor cancels the pending investment via `CancelInvestmentAsync`.
5. The test verifies the investment status changes to "Cancelled" or is removed from the active list.
6. The test verifies that the investor's balance is restored (funds released back).
7. Investor re-invests in the same project to confirm the wallet and project remain functional after cancellation.

## Key Validations

- Above-threshold investment stays at Step 1 (pending) until founder approval.
- `CancelInvestmentAsync` successfully cancels a pending investment.
- Investment status transitions to "Cancelled" after cancellation.
- Investor balance is restored after cancellation (funds released).
- Re-investment after cancellation succeeds, proving no state corruption.
- Project remains investable after a cancelled investment.

## Notes

- The test reuses the real headless Avalonia app and switches profiles by rebuilding DI per profile.
- Above-threshold investments are used specifically because they stay at Step 1 (pending) until founder approval, making them cancellable without needing the founder to sign first.
- Balance restoration is verified with a tolerance for transaction fees.
- The re-investment phase proves the full cycle is idempotent and the wallet/project state is clean after cancellation.
- IndexerLagTimeout (5 minutes) is used for polling because signet mempool propagation can be slow.

## Run

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~InvestmentCancellationTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~InvestmentCancellationTest" --logger "console;verbosity=detailed"
```

# InvestAndRecoverTest

## Purpose

Full end-to-end integration test that boots the real Avalonia app in headless mode, creates a wallet, funds it via a testnet faucet, creates an **Invest-type** project through the 6-step wizard, submits an investment, has the **founder approve the pending request**, has the **investor confirm the signed investment**, then has the **founder spend stage 1**, and finally has the **investor recover the remaining stages**.

## Scenario

1. Boot the app and wipe profile data.
2. Create a wallet with the Generate flow.
3. Faucet-fund the wallet until balance is non-zero.
4. Create an **Invest** project with:
   - target amount `1.0 BTC`
   - investment end date `now + 3 months`
   - 3 monthly generated stages
   - start date set in the past so stage 1 is claimable while later stages remain locked
5. Reload founder projects so `ProjectIdentifier` and `OwnerWalletId` are populated.
6. Find the project from SDK-backed Find Projects and verify metadata like project type and displayed target amount.
7. Invest `0.02 BTC`, add the investment to the portfolio, reload from SDK, and verify there is exactly one portfolio entry for the project.
8. Founder approves the pending investment request in **Funders**.
9. Investor confirms the signed investment in **Funded** and verifies it becomes active.
10. Founder opens **Manage Project**, verifies:
    - stage 1 is claimable now
    - stages 2 and 3 are locked
    - locked stages show `AvailableInDays`
    - stage 3 countdown is greater than stage 2 countdown
11. Founder spends stage 1.
12. Investor loads recovery status and verifies:
    - recovery stages are populated
    - stage 1 is `Released`
    - stages 2 and 3 are still unreleased
13. Investor executes the recovery action exposed by `RecoveryState.ActionKey`.

## Notes

- This is the single-profile companion to the multi-profile invest flow in `src/design/App.Test.Integration/MultiInvestClaimAndRecoverTest.cs`.
- It intentionally exercises VM assertions in addition to transaction success so duplicate rows, wrong stage states, or missing recovery-stage data fail fast.
- Invest projects do not use the fund penalty-threshold behavior for approval routing; they still require founder approval in the portfolio flow.
- The test relies on real Signet, faucet, indexer, and Nostr infrastructure, so indexer lag and mempool propagation are expected.

## Run

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "DisplayName~FullInvestAndRecoverFlow"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "DisplayName~FullInvestAndRecoverFlow" --logger "console;verbosity=detailed"
```

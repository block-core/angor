# MultiFundReleaseUnfundedAndClaimTest

## Purpose

Full end-to-end integration test for a **Fund** project with three isolated app profiles: one founder and two investors. It verifies mixed below-threshold and above-threshold investment behavior, founder approval, founder stage-1 claim across both investor UTXOs, founder release of remaining stages, and investor claims of the released stages without penalty.

## Profiles

- Founder profile: `MultiFundReleaseUnfundedAndClaim-Founder`
- Investor1 profile (below threshold): `MultiFundReleaseUnfundedAndClaim-Investor1`
- Investor2 profile (above threshold): `MultiFundReleaseUnfundedAndClaim-Investor2`

All profiles are created under:

- `C:\Users\dan\AppData\Local\Angor\Profiles\`

## Scenario

1. Founder profile creates a wallet, funds it, and deploys a **Fund** project.
2. The project uses:
   - approval threshold `0.01 BTC`
   - penalty days `0`
   - weekly payout day = today
   - 3 payout stages
3. Investor 1 invests `0.001 BTC` (below threshold, auto-approved).
4. Investor 2 invests `0.02 BTC` (above threshold, requires founder approval).
5. Founder approves the pending above-threshold investment.
6. Investor 2 confirms the signed investment so it becomes active.
7. Founder claims stage 1 and spends both stage-1 UTXOs together.
8. Founder releases the remaining stages back to investors from the Manage Project UI.
9. Investor 2 claims the remaining stages through the unfunded-release path.
10. Investor 1 claims the remaining stages through the below-threshold recovery path.

## Notes

- The test reuses the real headless Avalonia app and switches profiles by rebuilding DI per profile.
- It logs the active profile directory before each phase so profile isolation is explicit.
- It validates the missing #10 release-funds UI path for a **Fund** project, not just the existing **Invest** path.
- The regression this test protects is subtle: below-threshold Fund investments must stay auto-approved, but they still need release metadata so the founder can later release remaining stages back to the investor.
- Investor claims need fee funds in the investor wallet, so the test proactively refreshes and faucet-funds the wallet before each release transaction.

## Run

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~MultiFundReleaseUnfundedAndClaimTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~MultiFundReleaseUnfundedAndClaimTest" --logger "console;verbosity=detailed"
```

# MultiInvestReleaseUnfundedAndClaimTest

## Purpose

Full end-to-end integration test for an **Invest** project with three isolated app profiles: one founder and two investors. It verifies multi-investor investing, pending founder approval, founder approval of both investment requests, investor confirmation, founder stage-1 claim across both investor UTXOs, founder release signatures for the remaining stages, and investor claims of the remaining stages without penalty.

## Profiles

- Founder profile: `MultiInvestReleaseUnfundedAndClaim-Founder`
- Investor1 profile: `MultiInvestReleaseUnfundedAndClaim-Investor1`
- Investor2 profile: `MultiInvestReleaseUnfundedAndClaim-Investor2`

All profiles are created under:

- `C:\Users\dan\AppData\Local\Angor\Profiles\`

## Scenario

1. Founder profile creates a wallet, funds it, and deploys an **Invest** project.
2. The project uses:
   - target amount `1.0 BTC`
   - investment end date `now + 3 months`
   - 3 generated monthly stages
3. Investor 1 invests `0.02 BTC`.
4. Investor 2 invests `0.03 BTC`.
5. Each investor portfolio entry is verified to be pending founder approval after submit.
6. Founder opens **Funders**, verifies both investment requests are present, and approves both of them.
7. Each investor returns to **Funded** and confirms the approved investment so it becomes active.
8. Founder claims stage 1 and spends both stage-1 UTXOs together.
9. Founder releases the remaining stages back to investors by sending release signatures.
10. Investor 1 claims the remaining stages with the unfunded-release path.
11. Investor 2 claims the remaining stages with the unfunded-release path.

## Notes

- The test reuses the real headless Avalonia app and switches profiles by rebuilding DI per profile.
- It logs the active profile directory before each phase so profile isolation is explicit.
- It asserts the deployed project type, the generated 3-stage investment schedule, the amount stored in each investor portfolio entry, and the pending -> approved -> active approval lifecycle.
- Investor claims need fee funds in the investor wallet, so the test proactively refreshes and faucet-funds the wallet before each release transaction.
- The investor-side claim waits specifically for the recovery action key `unfundedRelease`, which confirms the founder release signatures are visible before claiming.

## Run

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~MultiInvestReleaseUnfundedAndClaimTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~MultiInvestReleaseUnfundedAndClaimTest" --logger "console;verbosity=detailed"
```

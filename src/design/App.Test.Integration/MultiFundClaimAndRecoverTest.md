# MultiFundClaimAndRecoverTest

## Purpose

Full end-to-end integration test for a **Fund** project with three isolated app profiles: one founder and two investors. It verifies mixed below-threshold and above-threshold investment behavior, founder approval, founder stage claim across both investor UTXOs, recovery to penalty, recovery from penalty, and below-threshold end-of-project recovery.

## Profiles

- Founder profile: `MultiFundClaimAndRecover-Founder`
- Investor1 profile (below threshold): `MultiFundClaimAndRecover-Investor1`
- Investor2 profile (above threshold): `MultiFundClaimAndRecover-Investor2`

All profiles are created under:

- `C:\Users\dan\AppData\Local\Angor\Profiles\`

## Scenario

1. Founder profile creates a wallet, funds it, and deploys a **Fund** project.
2. The project uses:
   - approval threshold `0.01 BTC`
   - penalty days `0`
   - weekly payout day = today
3. Investor 1 invests `0.001 BTC` (below threshold, direct publish).
4. Investor 2 invests `0.02 BTC` (above threshold, requires founder approval).
5. Founder approves the pending above-threshold investment.
6. Investor 2 confirms the signed investment so it becomes active.
7. Founder claims stage 1 and spends both stage-1 UTXOs together.
8. Investor 2 recovers remaining funds to penalty, then immediately recovers from penalty because penalty days are zero.
9. Investor 1 recovers remaining funds through the below-threshold end-of-project path.

## Notes

- The test reuses the real headless Avalonia app and switches profiles by rebuilding DI per profile.
- It logs the active profile directory before each phase so profile isolation is explicit.
- It adds recovery diagnostics only in the test layer. No SDK/shared behavior was changed for this flow.
- Recovery actions need fee funds in the investor wallet, so the test proactively refreshes/faucet-funds the wallet before each recovery step.
- The faucet amount used by the app test flow is now `2 BTC`, which is enough for this scenario without overfunding every wallet.
- The test validates more than success states: project metadata, project type, staged plan visibility in the invest flow, and stored invested amount after each investment are asserted explicitly.

## Run

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~MultiFundClaimAndRecoverTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~MultiFundClaimAndRecoverTest" --logger "console;verbosity=detailed"
```

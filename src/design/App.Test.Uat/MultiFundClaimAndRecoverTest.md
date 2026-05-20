# MultiFundClaimAndRecoverTest

## Purpose

Per-process UAT version of the integration `MultiFundClaimAndRecoverTest`. Full end-to-end test for a **Fund** project with three isolated OS processes: one founder and two investors. Verifies mixed below-threshold and above-threshold investment behavior, founder approval, founder stage claim across both investor UTXOs, recovery to penalty, recovery from penalty, and below-threshold end-of-project recovery.

## Architecture

- **Per-process isolation**: Each profile runs in a separate App.Desktop process with a real Avalonia window
- **HTTP orchestration**: `TestAutomationClient` → `AutomationServer` (localhost HTTP)
- **Env vars**: `ANGOR_TEST_API=1` + `ANGOR_TEST_API_PORT=<port>`

## Profiles

- Founder: `MultiFundClaimAndRecover-Founder`
- Investor1 (below threshold): `MultiFundClaimAndRecover-Investor1`
- Investor2 (above threshold): `MultiFundClaimAndRecover-Investor2`

## Scenario

1. Founder creates wallet, funds it, enables debug mode, deploys a **Fund** project.
2. Project uses: approval threshold `0.01 BTC`, penalty days `0`, weekly payout day = today.
3. Investor 1 invests `0.001 BTC` (below threshold, auto-approved, 6-stage pattern).
4. Investor 2 invests `0.02 BTC` (above threshold, requires founder approval, 3-stage pattern).
5. Founder approves the pending above-threshold investment.
6. Investor 2 confirms the signed investment (step 3).
7. Founder claims stage 1 (2 UTXOs).
8. Investor 2 recovers to penalty, then immediately recovers from penalty (penalty days = 0).
9. Investor 1 recovers via below-threshold end-of-project path.

## Run

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~MultiFundClaimAndRecoverTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~MultiFundClaimAndRecoverTest" --logger "console;verbosity=detailed"
```

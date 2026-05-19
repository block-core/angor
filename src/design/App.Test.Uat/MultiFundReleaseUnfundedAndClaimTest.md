# MultiFundReleaseUnfundedAndClaimTest

## Purpose

Per-process UAT version of the integration `MultiFundReleaseUnfundedAndClaimTest`. Full end-to-end test for a **Fund** project with three isolated OS processes: one founder and two investors. Verifies mixed below-threshold and above-threshold investment behavior, founder approval, founder stage-1 claim, founder release of remaining stages, and investor claims via unfunded-release/belowThreshold paths.

## Architecture

- **Per-process isolation**: Each profile runs in a separate App.Desktop process with a real Avalonia window
- **HTTP orchestration**: `TestAutomationClient` → `AutomationServer` (localhost HTTP)
- **Env vars**: `ANGOR_TEST_API=1` + `ANGOR_TEST_API_PORT=<port>`

## Profiles

- Founder: `MultiFundReleaseUnfundedAndClaim-Founder`
- Investor1 (below threshold): `MultiFundReleaseUnfundedAndClaim-Investor1`
- Investor2 (above threshold): `MultiFundReleaseUnfundedAndClaim-Investor2`

## Scenario

1. Founder creates wallet, funds it, enables debug mode, deploys a **Fund** project.
2. Project uses: approval threshold `0.01 BTC`, penalty days `0`, weekly payout day = today, 3 stages.
3. Investor 1 invests `0.001 BTC` (below threshold, auto-approved).
4. Investor 2 invests `0.02 BTC` (above threshold, requires founder approval).
5. Founder approves the pending above-threshold investment.
6. Investor 2 confirms (step 3).
7. Founder claims stage 1 (2 UTXOs).
8. Founder releases remaining stages to investors.
9. Investor 2 claims via unfunded-release path.
10. Investor 1 claims via belowThreshold path.

## Run

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~MultiFundReleaseUnfundedAndClaimTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~MultiFundReleaseUnfundedAndClaimTest" --logger "console;verbosity=detailed"
```

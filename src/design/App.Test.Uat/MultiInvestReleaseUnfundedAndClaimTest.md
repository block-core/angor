# MultiInvestReleaseUnfundedAndClaimTest

## Purpose

Per-process UAT version of the integration `MultiInvestReleaseUnfundedAndClaimTest`. Full end-to-end test for an **Invest** project with three isolated OS processes: one founder and two investors. Verifies multi-investor investing, pending founder approval, batch approval, investor confirmation, founder stage-1 claim, founder release signatures, and investor claims via unfunded-release path.

## Architecture

- **Per-process isolation**: Each profile runs in a separate App.Desktop process with a real Avalonia window
- **HTTP orchestration**: `TestAutomationClient` → `AutomationServer` (localhost HTTP)
- **Env vars**: `ANGOR_TEST_API=1` + `ANGOR_TEST_API_PORT=<port>`

## Profiles

- Founder: `MultiInvestReleaseUnfundedAndClaim-Founder`
- Investor1: `MultiInvestReleaseUnfundedAndClaim-Investor1`
- Investor2: `MultiInvestReleaseUnfundedAndClaim-Investor2`

## Scenario

1. Founder creates wallet, funds it, deploys an **Invest** project (target `1.0 BTC`, 3 monthly stages).
2. Investor 1 invests `0.02 BTC`.
3. Investor 2 invests `0.03 BTC`.
4. Founder batch-approves both investments.
5. Both investors confirm (step 2 → step 3).
6. Founder claims stage 1 (2 UTXOs).
7. Founder releases remaining stages to investors.
8. Both investors claim via unfunded-release path.

## Run

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~MultiInvestReleaseUnfundedAndClaimTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~MultiInvestReleaseUnfundedAndClaimTest" --logger "console;verbosity=detailed"
```

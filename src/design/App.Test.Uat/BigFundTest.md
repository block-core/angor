# BigFundTest

## Purpose

Large-scale end-to-end UAT test for a **Fund** project with 15 investors (mixed below-threshold and above-threshold). Each participant runs as its own App.Desktop OS process with a real Avalonia window, orchestrated by xUnit via HTTP automation endpoints.

## Architecture

- **Per-process isolation**: Each profile (founder + 15 investors) runs in a separate App.Desktop process
- **HTTP orchestration**: Test communicates with each process via `TestAutomationClient` → `AutomationServer` (localhost HTTP)
- **Environment variables**: `ANGOR_TEST_API=1` + `ANGOR_TEST_API_PORT=<port>` activate the automation server in each process
- **Port assignment**: `TestProcessHost` assigns unique ports to each process

## Profiles

- Founder: `BigFund-Founder`
- Investors 1-7 (below threshold): `BigFund-Investor1` through `BigFund-Investor7`
- Investors 8-15 (above threshold): `BigFund-Investor8` through `BigFund-Investor15`

## Scenario

1. Founder creates wallet, funds via faucet, enables debug mode, deploys a **Fund** project.
2. Project uses:
   - approval threshold `0.01 BTC`
   - penalty days `0` (debug mode)
   - weekly payout day = today
   - two installment patterns (3 and 6 stages)
3. Each of 15 investors:
   - Launches own process, wipes data, creates wallet, funds via faucet
   - Invests in the project (below-threshold investors: `0.001 BTC`, above-threshold: `0.02 BTC`)
   - Below-threshold investments are auto-approved (direct publish)
   - Above-threshold investments require founder approval → investor confirmation
4. Founder claims stage 1 (expects 15 UTXOs).
5. Above-threshold investors recover to penalty, then recover from penalty (penalty days = 0).
6. Below-threshold investors recover via end-of-project path.

## Key Implementation Details

- Investors are processed sequentially (each creates wallet + invests + optionally gets approved)
- Above-threshold approval happens immediately after each above-threshold investment
- `TestProcessHost` manages process lifecycle; processes are disposed in `finally` block
- Founder process stays alive for the entire test duration
- Investor processes stay alive until all recovery actions complete

## Duration

~18 minutes on signet (dominated by faucet funding + blockchain confirmation waits)

## Run

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~BigFundTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~BigFundTest" --logger "console;verbosity=detailed"
```

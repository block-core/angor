# BigInvestTest

## Purpose

Large-scale end-to-end UAT test for an **Invest** project with 15 investors. Each participant runs as its own App.Desktop OS process with a real Avalonia window, orchestrated by xUnit via HTTP automation endpoints. Tests the full lifecycle: deploy → invest → batch approve → confirm → founder claim stage 1 → founder release remaining → investor claim unfunded release.

## Architecture

- **Per-process isolation**: Each profile (founder + 15 investors) runs in a separate App.Desktop process
- **HTTP orchestration**: Test communicates with each process via `TestAutomationClient` → `AutomationServer` (localhost HTTP)
- **Environment variables**: `ANGOR_TEST_API=1` + `ANGOR_TEST_API_PORT=<port>` activate the automation server in each process
- **Port assignment**: `TestProcessHost` assigns unique ports to each process

## Profiles

- Founder: `BigInvest-Founder`
- Investors 1-15: `BigInvest-Investor1` through `BigInvest-Investor15`

## Scenario

1. Founder creates wallet, funds via faucet, deploys an **Invest** project.
2. Project uses:
   - target amount `1.0 BTC`
   - investment end date `now + 3 months`
   - 3 generated monthly stages
3. All 15 investors each:
   - Launch own process, wipe data, create wallet, fund via faucet
   - Invest varying amounts (`0.02`–`0.04 BTC` cycling through 5 values)
   - All investments require founder approval (above threshold)
4. Founder batch-approves all 15 pending investments at once.
5. All 15 investors confirm their approved investments (step 2 → step 3).
6. Founder claims stage 1 (expects 15 UTXOs).
7. Founder releases remaining stages back to investors.
8. All 15 investors claim remaining stages via unfunded-release path.

## Key Implementation Details

- All investments submitted first, then batch-approved in one call (`Batch = true`)
- `ApproveInvestmentsAsync` uses reflection to call `ApproveSignatureAsync` on each pending signature
- `InvestInProjectAsync` drains all pages via `LoadMore()` loop before searching Projects collection
- Investor processes stay alive until unfunded release completes

## Duration

~17 minutes on signet (dominated by faucet funding + blockchain confirmation waits)

## Run

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~BigInvestTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~BigInvestTest" --logger "console;verbosity=detailed"
```

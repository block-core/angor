# CreateProjectTest

## Purpose

Full end-to-end integration test that boots the real Avalonia app in headless mode, creates a wallet, funds it via a testnet faucet, then walks through the 6-step project creation wizard to deploy an investment-type project — verifying the entire founder flow through the UI.

## Prerequisites

- Internet connectivity (testnet indexer + faucet API)
- Profile isolation: uses `test-send-receive` profile (`~/.local/share/App/Profiles/test-send-receive/`)
- Testnet faucet must be operational at `https://faucettmp.angor.io`

## Tests

### `FullCreateInvestmentProjectFlow`

| | |
|---|---|
| **Type** | End-to-end integration |
| **Network** | Signet testnet (real indexer + faucet + Nostr relays) |
| **Duration** | 60–180s |

**Verifies**: The complete founder project creation lifecycle — wallet setup, funding, 6-step wizard navigation, SDK project deployment (keys, Nostr profile, project info, blockchain tx), and project list verification.

**Steps**:

1. **Boot app** — Create a headless `Window` containing the full `ShellView` with real DI services.
2. **Wipe data** — Navigate to Settings and call `ConfirmWipeData()` to delete any wallets/settings from a previous run.
3. **Create wallet (Generate path)** — Navigate to Funds, click "Add Wallet" → "Generate New" → "Download Seed" (no-op in headless) → "Continue" → wait for success → "Done".
4. **Fund wallet** — Click `WalletCardBtnFaucet`, wait 5s, then poll `WalletCardBtnRefresh` until `TotalBalance != "0.0000"` (up to 120s).
5. **Navigate to My Projects** — Open the section, verify `MyProjectsViewModel` is available.
6. **Open create wizard** — Call `ResetWizard()` + `LaunchCreateWizard()` on the ViewModel, wire the `OnProjectDeployed` callback.
7. **Wizard Step 1 (Project Type)** — Dismiss welcome screen, select "investment" type via `SelectProjectType("investment")`, advance to step 2.
8. **Wizard Step 2 (Project Profile)** — Set `ProjectName` = "Test Investment Project", `ProjectAbout` = description, advance.
9. **Wizard Step 3 (Project Images)** — Skip (images are optional), advance.
10. **Wizard Step 4 (Funding Config)** — Set `TargetAmount` = "1" (BTC), `InvestEndDate` = 6 months from now, advance.
11. **Wizard Step 5 (Stages)** — Dismiss step 5 welcome, set `DurationValue` = "6", `DurationUnit` = "Months", `ReleaseFrequency` = "Monthly", call `GenerateInvestmentStages()`, verify stages generated, advance.
12. **Wizard Step 6 (Review & Deploy)** — Set `SimplePasswordProvider` key to `"default-key"`, call `Deploy()`, wait for wallet selector, select first wallet via `SelectWallet()`, call `PayWithWallet()`, poll until `DeployScreen.Success` (up to 120s).
13. **Complete deploy** — Call `GoToMyProjects()` on the deploy VM, hide shell modal.
14. **Verify project** — Assert wizard is closed, `HasProjects` is true, find project with name "Test Investment Project" in the list, verify type = "investment".

**Key implementation details**:

- **Hybrid UI/VM approach**: Uses AutomationId-based helpers for wallet creation/funding (same as SendToSelfTest), but drives the wizard and deploy flow primarily through ViewModel methods because:
  - Type selection cards (Step 1) are `Border` elements with `PointerPressed` handlers — no Button to click
  - Wallet selection in deploy overlay uses `Border.PointerPressed`
  - ListBox frequency presets require programmatic selection
- **Deploy callback wiring**: The test manually wires `OnProjectDeployed` (normally done by `MyProjectsView.OpenCreateWizard` code-behind) to ensure the project is added to the list after deploy.
- **Password provider**: Must set `SimplePasswordProvider` to `"default-key"` before deploy — matches the encryption key used during wallet creation.
- **Real SDK pipeline**: The deploy step exercises the full SDK pipeline — `CreateProjectKeys`, `CreateProjectProfile`, `CreateProjectInfo`, `CreateProject` (blockchain tx), `SubmitTransactionFromDraft`.

**How to run**:
```bash
dotnet test src/design/App.Test.Integration/App.Test.Integration.csproj \
  --filter "DisplayName~FullCreateInvestmentProjectFlow" \
  --logger "console;verbosity=detailed"
```

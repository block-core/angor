# Integration Test Analysis — Interaction Hierarchy Audit

## Overview

This document summarizes an audit of all integration tests in `App.Test.Integration` against the [Interaction Hierarchy](TESTING_GUIDELINES.md#interaction-hierarchy-ui--viewmodel--sdk) defined in the testing guidelines. The goal is to identify where tests bypass the UI layer and call ViewModels or SDK services directly, find missing validations, and recommend improvements to increase test reliability.

---

## 1. ViewModel-Level Calls (Where Tests Bypass UI)

### Navigation (all tests)

Every test navigates via `window.NavigateToSection("...")` which sets `shellVm.SelectedNavItem` directly (`TestHelpers.cs:194-199`). There is no UI-level click on the sidebar nav items.

**Impact**: If the nav sidebar binding or click handler breaks, no test will catch it.

### Create Project Wizard (CreateProjectTest, InvestAndRecoverTest, FundAndRecoverTest, WalletImportAndProjectScanTest, Multi* tests)

The entire 6-step wizard is driven exclusively through ViewModel calls:

| Call | Files |
|---|---|
| `wizardVm.DismissWelcome()` | All wizard tests |
| `wizardVm.SelectProjectType("investment"/"fund")` | All wizard tests |
| `wizardVm.GoNext()` | All wizard tests |
| `wizardVm.ProjectName = ...` | All wizard tests |
| `wizardVm.TargetAmount = ...` | All wizard tests |
| `wizardVm.GenerateInvestmentStages()` / `GeneratePayoutSchedule()` | All wizard tests |
| `wizardVm.Deploy()` | All wizard tests |

**Why**: `TestHelpers.cs:527` explains — the EmptyState button and wizard type cards use `PointerPressed` on `Border` elements, which the headless test harness can't simulate via `RaiseEvent(Button.ClickEvent)`.

### Deploy Flow (all deploy tests)

| Call | Location |
|---|---|
| `deployVm.SelectWallet(wallet)` | CreateProjectTest:329, InvestAndRecoverTest:150, FundAndRecoverTest, WalletImportAndProjectScanTest, Multi* |
| `deployVm.PayWithWallet()` | Same files |
| `deployVm.GoToMyProjects()` | Same files |

**Why**: Wallet cards use `PointerPressed` on `Border`, same limitation.

### Invest Flow (FindProjectsPanelTests, InvestModalsViewFixesTest, InvestAndRecoverTest, FundAndRecoverTest, Multi*)

| Call | Location |
|---|---|
| `investVm.Submit()` | FindProjectsPanelTests:262, InvestAndRecoverTest:256, etc. |
| `investVm.SelectWallet(wallet)` | FindProjectsPanelTests:366, InvestAndRecoverTest:261, etc. |
| `investVm.PayWithWallet()` | InvestAndRecoverTest:266, FundAndRecoverTest, Multi* |
| `investVm.ShowInvoice()` | FindProjectsPanelTests:432, InvestModalsViewFixesTest:75 |
| `investVm.SelectNetworkTab(...)` | InvestModalsViewFixesTest:107, 138-142 |
| `investVm.AddToPortfolio()` | InvestAndRecoverTest:290, FundAndRecoverTest, Multi* |
| `investVm.CloseModal()` | FindProjectsPanelTests:377, 418, 454 |
| `investVm.BackToWalletSelector()` | FindProjectsPanelTests:440 |
| `investVm.SelectQuickAmount(0.01)` | FindProjectsPanelTests:233 |

### Funder Approval (InvestAndRecoverTest, Multi*)

| Call | Location |
|---|---|
| `fundersVm.SetFilter("waiting"/"approved")` | InvestAndRecoverTest:324, 351 |
| `fundersVm.ApproveSignature(id)` | InvestAndRecoverTest:343 |
| `fundersVm.LoadInvestmentRequestsAsync()` | InvestAndRecoverTest:330, 350 |

**Why**: The `ClickApproveSignatureAsync` UI helper exists in `TestHelpers.cs:671` but is only used in Multi* tests. `InvestAndRecoverTest` calls the VM directly.

### Portfolio Operations (InvestAndRecoverTest, FundAndRecoverTest, Multi*)

| Call | Location |
|---|---|
| `portfolioVm.ConfirmInvestmentAsync(...)` | InvestAndRecoverTest:390 |
| `portfolioVm.LoadRecoveryStatusAsync(...)` | InvestAndRecoverTest:536 |
| `portfolioVm.LoadInvestmentsFromSdkAsync()` | InvestAndRecoverTest:297 |

### Balance Reads (SendToSelfTest, TestHelpers, InvestAndRecoverTest, WalletImportAndProjectScanTest)

`fundsVm.TotalBalance` is read from the ViewModel ~8 times across tests instead of reading from a UI TextBlock. This misses binding regressions.

| Location | What's read |
|---|---|
| `TestHelpers.cs:490,518` | `fundsVm.TotalBalance` in FundWalletViaFaucet |
| `SendToSelfTest:176,188` | `fundsVm.TotalBalance` in final balance check |
| `WalletImportAndProjectScanTest:302,313` | `fundsVm.TotalBalance` in balance discovery |
| `CreateProjectTest:157` | `fundsVm.TotalBalance` in header sync check |

---

## 2. SDK/Service-Level Direct Calls

### Justified (have comments)

| Location | Service | Comment |
|---|---|---|
| `CreateProjectTest:448-449` | `IProjectAppService`, `IWalletAppService` | "DIRECT SDK CALL: No ViewModel exposes the raw ProjectDto..." |
| `SendToSelfTest:141` | `IWalletContext` via DI | Step 8b: verify pending balance internal accounting |

### Unjustified (missing comments) — NOW FIXED for InvestAndRecoverTest, pending for Multi*

| Location | Service | Why it's called |
|---|---|---|
| `InvestAndRecoverTest:293` | `GetRequiredService<PortfolioViewModel>()` | DI resolve instead of navigating to Funded section |
| `InvestAndRecoverTest:605` | `IWalletAppService.RefreshAndGetAccountBalanceInfo()` | Raw sats check for fee funding |
| `MultiFundClaimAndRecoverTest:470,825` | `GetRequiredService<PortfolioViewModel>()` | Same pattern |
| `MultiFundClaimAndRecoverTest:868` | `IWalletAppService.RefreshAndGetAccountBalanceInfo()` | Same pattern |
| `MultiInvestClaimAndRecoverTest:402,827` | `GetRequiredService<PortfolioViewModel>()` | Same pattern |
| `MultiInvestClaimAndRecoverTest:870` | `IWalletAppService.RefreshAndGetAccountBalanceInfo()` | Same pattern |
| `MultiFundReleaseUnfundedAndClaimTest:412,755` | `GetRequiredService<PortfolioViewModel>()` | Same pattern |
| `MultiFundReleaseUnfundedAndClaimTest:798` | `IWalletAppService.RefreshAndGetAccountBalanceInfo()` | Same pattern |
| `MultiInvestReleaseUnfundedAndClaimTest:414,761` | `GetRequiredService<PortfolioViewModel>()` | Same pattern |
| `MultiInvestReleaseUnfundedAndClaimTest:804` | `IWalletAppService.RefreshAndGetAccountBalanceInfo()` | Same pattern |
| `FundAndRecoverTest:408` | `GetRequiredService<PortfolioViewModel>()` | Same pattern |
| `InvestmentCancellationTest:535,574` | `GetRequiredService<PortfolioViewModel>()` | Same pattern |
| `OneClickInvestLightningTest:211` | `GetRequiredService<PortfolioViewModel>()` | Same pattern |

### Acceptable Infrastructure Calls

| Location | Service | Purpose |
|---|---|---|
| All tests | `GetRequiredService<SimplePasswordProvider>()` | Test setup — set decryption key |
| `WalletImportAndProjectScanTest:508` | `GetRequiredService<ProfileContext>()` | Test infrastructure — verify profile isolation |

---

## 3. UI-Level Assertions Already Present (Good Examples)

| Test | What's checked at UI level |
|---|---|
| `SendToSelfTest` | `ReceiveAddressText` TextBlock, `SendFormPanel`, `SendSuccessPanel`, `SummaryTxid`, `AmountError`, `FeeConfirmButton` |
| `TestHelpers.CreateWalletViaGenerate()` | `ChoicePanel`, `BackupPanel`, `CreateWalletSuccessPanel`, `ContinueBtnSpinner` visibility, modal closed state |
| `FindProjectsPanelTests` | `ProjectListPanel`/`ProjectDetailPanel`/`InvestPagePanel` visibility, `ProjectCard` properties, `InvestButton` border visibility, `ProjectDetailView` DataContext |
| `TestHelpers.ClickRecoveryFlowAsync()` | `InvestmentDetailView` visibility, `RecoverFundsButton`, `ConfirmRecoveryModal`, `FeeConfirmButton`, success modal state |
| `TestHelpers.ClickManageProjectClaimStageAsync()` | `StageClaimBtn` by class+tag, `ClaimSelectedBtn`, `FeeConfirmButton` |
| `WalletImportAndProjectScanTest` | `SeedPhraseDisplay` TextBlock, `ImportPanel`, `SeedPhraseInput`, `CreateWalletSuccessPanel` |

---

## 4. Missing Validations — Specific Improvement Opportunities

### A. Navigation never verified at UI level

**Every** `NavigateToSection("X")` call (40+ occurrences) never asserts the target view appeared in the visual tree.

**Suggestion**: After navigation, `WaitForControl<FundsView/MyProjectsView/etc.>` and assert it's visible. Catches broken navigation bindings.

### B. Balance reads use VM instead of UI TextBlock

No balance TextBlock has an `AutomationId` or `x:Name` currently. Tests read `fundsVm.TotalBalance` instead.

**Suggestion**: Add `AutomationId="TotalBalanceText"` to the FundsView total balance TextBlock, `AutomationId="HeaderAvailableBalance"` to ShellView header. Then read via `window.GetText("TotalBalanceText")`.

### C. Wizard step transitions not verified visually

When `wizardVm.GoNext()` advances to step N, the test checks `wizardVm.CurrentStep.Should().Be(N)` but never verifies the step N panel is visible.

**Suggestion**: Add `AutomationId="WizardStep1Panel"` through `"WizardStep6Panel"` to the wizard view, then assert visibility after each `GoNext()`.

### D. Deploy/invest success screens not verified visually

Tests check `deployVm.CurrentScreen == DeployScreen.Success` (VM) but never find the success panel in the visual tree.

**Suggestion**: `DeployFlowOverlay.axaml` has no AutomationIds at all. Add `AutomationId="DeploySuccessPanel"`, `AutomationId="DeployStatusText"`. The invest flow already has `InvestSuccessModal` — use it.

### E. InvestModalsViewFixesTest has zero UI assertions

This 180-line test checks only VM properties (`vm.HasError`, `vm.CurrentScreen`, `vm.PaymentStatusText`, `vm.InvoiceFieldLabel`, etc.) and never finds any control in the visual tree.

**Suggestion**: After `ShowInvoice()`, verify the Invoice panel is visible via `WaitForControl`. Check that error banner TextBlock shows expected text. Verify tab icon controls change.

### F. Funder approval flow missing UI checks (InvestAndRecoverTest)

After navigating to Funders section (line 318), never verifies `FundersView` appeared. Calls `fundersVm.ApproveSignature()` directly instead of using the existing `TestHelpers.ClickApproveSignatureAsync()` helper.

**Suggestion**: Use the UI helper that already exists.

### G. Portfolio section never verified visually

After navigating to "Funded" (InvestAndRecoverTest:365), never checks that `PortfolioView` appeared or that investment cards rendered. All assertions are on VM properties.

**Suggestion**: Wait for `PortfolioView`, then find investment items in the visual tree and verify count + text.

### H. Project list count not validated at UI level

After deploy, `myProjectsVm.HasProjects` is checked but the actual number of rendered project cards is never verified.

**Suggestion**: Count `MyProjectCard` (or equivalent) controls in the visual tree.

### I. Missing text content assertions on success screens

| Where | What's missing |
|---|---|
| After wallet creation | Wallet name/label text on WalletCard never verified |
| After send-to-self | Transaction amount in success panel never verified |
| After deploy | Project name in success screen never verified |
| After invest | Investment amount in success screen never verified |
| After recovery | Recovery amount in success modal never verified |

### J. No error state assertions on happy-path tests

Happy-path tests never assert that error controls are **not** visible. If an error banner accidentally shows alongside a success, no test catches it.

**Suggestion**: After success states, assert `ErrorMessage` is null AND the error banner control is not visible.

---

## 5. AutomationId Gaps (Need Adding to AXAML)

These views currently lack AutomationIds needed by tests:

| View | What needs AutomationId |
|---|---|
| `FundsView.axaml` | Total balance TextBlock |
| `ShellView.axaml` | Header available/invested balance TextBlocks, nav items |
| `CreateProjectView.axaml` (wizard) | Step panels (1-6) |
| `DeployFlowOverlay.axaml` | All panels: WalletSelector, Deploying, Success. Status text, project name in success |
| `MyProjectsView.axaml` | Project list container, empty state |
| `PortfolioView.axaml` | Investment list container, empty state, investment cards |
| `FundersView.axaml` | Signature request list, filter buttons |
| `WalletCard` | Balance text display |
| `ManageProjectContentView.axaml` | Stage list, available balance |

---

## 6. Summary of Priorities

| Priority | Improvement | Impact |
|---|---|---|
| **High** | Add AutomationIds to balance TextBlocks, wizard steps, deploy/invest success, section root panels | Enables all other UI assertions |
| **High** | Add UI panel visibility checks after navigation | Catches broken nav/binding |
| **High** | Read balance from UI TextBlock instead of VM property | Catches binding regressions |
| **High** | Add text content assertions to success screens | Catches display regressions |
| **Medium** | Add justifying comments to unjustified SDK calls | Guideline compliance |
| **Medium** | Convert InvestModalsViewFixesTest to include UI assertions | Currently VM-only |
| **Medium** | Use existing `ClickApproveSignatureAsync` helper in InvestAndRecoverTest | Consistency |
| **Low** | Replace `NavigateToSection` VM call with UI sidebar click | Requires nav AutomationIds |
| **Low** | Extract duplicated wizard/deploy/invest code from Multi* tests into TestHelpers | Code dedup |

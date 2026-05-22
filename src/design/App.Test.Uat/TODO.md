# UAT Automation TODO

Track work needed to move from VM/service bypasses to fully UI-driven automation.

## 1. Replace PointerPressed with Button.Command ✅ DONE

Converted all actionable `Border.PointerPressed` handlers to `Button Classes="Unstyled"` + Click handlers.

### Completed conversions
- [x] WalletSwitcherModal — wallet cards
- [x] DeployFlowOverlay — wallet cards
- [x] InvestPageView — submit, quick-amount, sub-plan, fund-pattern, copy buttons
- [x] CreateProjectStep1View — type cards (invest, fund, subscription)
- [x] CreateProjectStep5View — payout frequency, installment toggles
- [x] FeeSelectionPopup — fee priority buttons
- [x] PaymentFlowView — wallet cards, network tabs (OnChain/Lightning/Import)
- [x] ProjectDetailView — invest, nav CTA, share, copy, explorer, view JSON buttons
- [x] ShareModal — social media buttons, email button
- [x] SettingsView — indexer default toggle checkbox
- [x] RecoveryModalsView — copy claim project ID button

### Kept as PointerPressed (structural patterns, not simple button actions)
- ManageProjectModalsView: UTXO item selection (tunnel routing), explorer link (bubble routing), backdrop
- WalletDetailModal: dynamically created UTXO card selection
- ProjectDetailView: collapsible section toggle (positional hit-testing)
- ShellView, SettingsView: modal backdrop close, content propagation stop

## 2. Add Control Names / AutomationIds ✅ DONE

Controls already have `Name` attributes that the automation server can find via `FindByName`.
Added explicit Names to previously-unnamed inputs:

- [x] `InvestTargetAmountInput` — investment type target amount (Step 4)
- [x] `FundTargetAmountInput` — fund type target amount (Step 4)
- [x] `ApprovalThresholdInput` — fund approval threshold (Step 4)
- [x] `DurationValueInput` — investment duration (Step 5)
- [x] `ProfileNameTextBox` — edit profile username
- [x] `ProfileDisplayNameTextBox` — edit profile display name
- [x] `ProfileAboutTextBox` — edit profile about
- [x] `ProfilePictureTextBox` — edit profile picture URL
- [x] `ProfileBannerTextBox` — edit profile banner URL
- [x] `ProfileWebsiteTextBox` — edit profile website URL
- [ ] Subscription price input (Step 4) — needs Name

### Already named (no changes needed)
- Wizard nav: `StartButton`, `NextStepButton`, `PrevStepButton`, `DeployButton`, `Step5WelcomeButton`
- Step 1: `TypeInvestCard`, `TypeFundCard`, `TypeSubscriptionCard`
- Step 2: `ProjectNameTextBox`, `AboutTextBox`
- Step 3: `BannerUrlTextBox`, `ProfileUrlTextBox`
- Step 4: `InvestStartDatePicker`, `InvestEndDatePicker`
- Step 5: `PayoutFreqMonthly`, `PayoutFreqWeekly`, `Installment3/6/9`, `GeneratePayoutsButton`, `GenerateStagesButton`, `DurationUnitCombo`
- Invest page: `AmountInput`, `SubmitButton`, `MobileSubmitButton`, `FundPatternButton`
- Payment flow: `WalletButton`, `PayWithWalletButton`, `SuccessActionButton`
- Portfolio: `RefreshButton`, `ManageButton`, `ConfirmInvestmentButton`, `RecoverFundsButton`
- Recovery modals: `ConfirmRecoveryModal`, `ClaimPenaltyButton`, `ConfirmReleaseModal`
- Fee popup: `ConfirmButton`, `FeePriority`, `FeeStandard`, `FeeEconomy`
- Funders: `ApproveButton`, `MobileApproveButton`, `ApproveAllButton`
- Edit profile: `SaveButton`, `TabProject`, `ProjectContentBox`

## 3. Replace VM Method Calls with UI Actions ✅ DONE

### Create Project wizard (fund + invest) ✅
- [x] `DismissWelcome()` → click `StartButton`
- [x] `SelectProjectType()` → click `TypeFundCard` / `TypeInvestCard`
- [x] `GoNext()` → click `NextStepButton`
- [x] `ProjectName`/`ProjectAbout` → type into `ProjectNameTextBox`/`AboutTextBox`
- [x] `BannerUrl`/`ProfileUrl` → type into `BannerUrlTextBox`/`ProfileUrlTextBox`
- [x] `TargetAmount` → type into `FundTargetAmountInput`/`InvestTargetAmountInput`
- [x] `ApprovalThreshold` → type into `ApprovalThresholdInput`
- [x] `DismissStep5Welcome()` → click `Step5WelcomeButton`
- [x] `PayoutFrequency`/`ToggleInstallmentCount()` → click `PayoutFreqWeekly`/`Installment3`/`Installment6`
- [x] `GeneratePayoutSchedule()`/`GenerateInvestmentStages()` → click `GeneratePayoutsButton`/`GenerateStagesButton`
- [x] `DurationValue` → type into `DurationValueInput`

### Invest flow ✅
- [x] `InvestmentAmount` → type into `AmountInput`
- [x] `Submit()` → click `SubmitButton`
- [x] `SelectWallet` → click first `WalletButton`
- [x] `PayWithWalletCommand` → click `PayWithWalletButton`
- [x] `AddToPortfolio()` → click `SuccessActionButton`
- [x] `SelectFundingPattern` → click `FundPatternButton` by StageCount match

### Approve investments ✅
- [x] Reflection `ApproveSignatureAsync` → click `ApproveButton` (fallback to VM's public `ApproveSignature`)

### Confirm investment ✅
- [x] Navigate to Funded → click `RefreshButton` → click `ManageButton` → click `ConfirmInvestmentButton`

### Recovery flow ✅
- [x] Navigate to Funded → click `ManageButton` → click `RecoverFundsButton`
- [x] Click appropriate modal confirm button (ConfirmRecoveryModal/ClaimPenaltyButton/ConfirmReleaseModal)
- [x] Click `ConfirmButton` in FeeSelectionPopup (defaults to "standard" 20 sat/vB)

### Edit profile flow ✅
- [x] Type into named TextBoxes (ProfileNameTextBox, ProfileDisplayNameTextBox, etc.)
- [x] Click `TabProject` to switch tab for ProjectContent
- [x] Click `SaveButton` to save

### Still using VM property sets (no UI input available — pragmatic bypasses)
- `PenaltyDays = 0` (fund type defaults, no UI control)
- `InvestEndDate` (CalendarDatePicker — complex to automate via clicks)
- `DurationUnit` (ComboBox selection)
- `ReleaseFrequency` (ListBox selection)
- `StartDate` (no UI input)
- `WeeklyPayoutDay` (ListBox selection)
- `OpenProjectDetail` / `OpenInvestPage` (project card uses Tapped event, not Button)

## 4. Expose Seed Words Without Native File Picker ✅ DONE

- [x] `MarkSeedDownloaded()` internal method bypasses native file dialog
- [x] `GeneratedSeedWords` internal property exposes seed words
- [x] Full UI flow: BtnGenerate → MarkSeedDownloaded → BtnContinueBackup → wait success → BtnCreateWalletDone

## 5. Replace Reflection Hacks ✅ DONE

- [x] `_generatedSeedWords` — replaced with `GeneratedSeedWords` internal property
- [x] `ApproveSignatureAsync` — replaced with `ApproveButton` click + public `ApproveSignature(id)` fallback

## 6. Replace Direct DI Service Calls (lowest priority)

- [ ] `BlossomUploadService` calls → drive through upload UI
- [ ] `IProjectAppService` direct queries → drive through browse/refresh UI
- [x] `PortfolioViewModel.ConfirmInvestmentAsync` → click `ConfirmInvestmentButton`
- [x] `PortfolioViewModel.RecoverFundsAsync` etc. → click recovery modal buttons + fee popup

## Priority Order

1. ~~PointerPressed → Button.Command~~ ✅
2. ~~Add AutomationIds / Names~~ ✅
3. ~~Replace VM method calls~~ ✅ (all flows done)
4. ~~Seed words exposure~~ ✅
5. ~~Replace reflection hacks~~ ✅
6. **Replace DI service calls** (Blossom upload, project queries — may stay as pragmatic bypasses)

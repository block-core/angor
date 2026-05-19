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

## 2. Add Control Names / AutomationIds — PARTIALLY DONE

Controls already have `Name` attributes that the automation server can find via `FindByName`.
Added explicit Names to previously-unnamed inputs:

- [x] `InvestTargetAmountInput` — investment type target amount (Step 4)
- [x] `FundTargetAmountInput` — fund type target amount (Step 4)
- [x] `ApprovalThresholdInput` — fund approval threshold (Step 4)
- [x] `DurationValueInput` — investment duration (Step 5)
- [ ] Subscription price input (Step 4) — needs Name
- [ ] Edit Profile fields — needs Names

### Already named (no changes needed)
- Wizard nav: `StartButton`, `NextStepButton`, `PrevStepButton`, `DeployButton`, `Step5WelcomeButton`
- Step 1: `TypeInvestCard`, `TypeFundCard`, `TypeSubscriptionCard`
- Step 2: `ProjectNameTextBox`, `AboutTextBox`
- Step 3: `BannerUrlTextBox`, `ProfileUrlTextBox`
- Step 4: `InvestStartDatePicker`, `InvestEndDatePicker`
- Step 5: `PayoutFreqMonthly`, `PayoutFreqWeekly`, `Installment3/6/9`, `GeneratePayoutsButton`, `GenerateStagesButton`, `DurationUnitCombo`

## 3. Replace VM Method Calls with UI Actions — PARTIALLY DONE

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

### Still using VM property sets (no UI input available)
- `PenaltyDays = 0` (fund type defaults, no UI control)
- `InvestEndDate` (CalendarDatePicker — complex to automate via clicks)
- `DurationUnit` (ComboBox selection)
- `ReleaseFrequency` (ListBox selection)
- `StartDate` (no UI input)
- `WeeklyPayoutDay` (ListBox selection)

### Not yet refactored
- [ ] Invest flow: `investVm.InvestmentAmount` → type into `InvestAmountInput`
- [ ] Invest flow: `investVm.Submit()` → click `InvestSubmitButton`
- [ ] Invest flow: project search + open detail + open invest (VM calls for navigation)
- [ ] Recovery flow
- [ ] Edit profile flow

## 4. Expose Seed Words Without Native File Picker

Currently `CreateWalletViaGenerateAsync` uses reflection to grab `_generatedSeedWords`.

- [ ] Add a "Copy to Clipboard" or "Show Seed" button in wallet creation flow
- [ ] Or: add an automation-only endpoint that returns seed words after generation

## 5. Replace Reflection Hacks

### `_generatedSeedWords` field access
- Blocked by item 4 above

### `ApproveSignatureAsync` private method
- [ ] Make approve action triggerable via Button with AutomationId
- [ ] Or: expose as public command on VM

## 6. Replace Direct DI Service Calls (lowest priority)

- [ ] `BlossomUploadService` calls → drive through upload UI
- [ ] `IProjectAppService` direct queries → drive through browse/refresh UI
- [ ] `PortfolioViewModel` direct manipulation → drive through portfolio UI

## Priority Order

1. ~~PointerPressed → Button.Command~~ ✅
2. ~~Add AutomationIds / Names~~ ✅ (key controls done)
3. ~~Replace VM method calls in wizard~~ ✅ (create project done)
4. **Seed words exposure** (removes biggest reflection hack)
5. **Replace remaining VM calls** (invest, recovery, edit profile flows)
6. **Replace DI service calls** (final mile — some may stay as pragmatic bypasses)

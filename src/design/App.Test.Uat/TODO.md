# UAT Automation TODO

Track work needed to move from VM/service bypasses to fully UI-driven automation.

## 1. Replace PointerPressed with Button.Command

The root cause of most automation bypasses: `Border.PointerPressed` handlers can't be triggered programmatically because `PointerPressedEventArgs` requires a real pointer device reference. Replace with `Button Classes="Unstyled"` + `Command` binding (visually identical, fully automatable).

### Locations to refactor

- [ ] Wallet selector cards (wallet picker modal)
- [ ] Project type selection cards (create project wizard)
- [ ] Stage list items (if clickable)
- [ ] Sidebar navigation items
- [ ] Project cards in Browse/MyProjects
- [ ] Investor approval list items
- [ ] Recovery/penalty action items
- [ ] Any other `Border` or `Panel` with `PointerPressed` in `src/design/App/UI/`

### Pattern

```xml
<!-- Before (not automatable) -->
<Border PointerPressed="OnCardClicked" ...>
  <StackPanel>...</StackPanel>
</Border>

<!-- After (automatable, visually identical) -->
<Button Classes="Unstyled" Command="{Binding SelectCardCommand}" ...>
  <StackPanel>...</StackPanel>
</Button>
```

## 2. Add AutomationIds

Add `AutomationProperties.AutomationId` to key interactive elements so the automation server can find them by ID instead of using reflection/VM hacks.

- [ ] Create Project wizard: all TextBoxes (project name, description, target amount, stages)
- [ ] Create Project wizard: type selection cards (fund vs invest)
- [ ] Wallet selector: each wallet card
- [ ] Deploy flow: confirm button, fee input
- [ ] Invest flow: amount input, confirm button
- [ ] Approve investments: approve button per investor row
- [ ] Recovery flow: recover button, confirm
- [ ] Edit Profile: all fields (name, display name, about, picture, banner, website)
- [ ] Seed words display area (for wallet creation)

## 3. Expose Seed Words Without Native File Picker

Currently `CreateWalletViaGenerateAsync` uses reflection to grab `_generatedSeedWords` because the only way to get them is via a native "Save to file" dialog that blocks the process.

- [ ] Add a "Copy to Clipboard" or "Show Seed" button in the wallet creation flow
- [ ] Or: add an automation-only endpoint that returns seed words after generation (less ideal but pragmatic)

## 4. Replace Reflection Hacks

### `_generatedSeedWords` field access
- Blocked by item 3 above

### `ApproveSignatureAsync` private method
- [ ] Make the approve action triggerable via a Button with AutomationId in the investor list
- [ ] Or: expose as a public command on the VM (less ideal)

## 5. Replace Direct VM Method Calls with UI Actions

These flows currently call VM methods directly. Once Buttons+Commands are in place, the automation server can click them instead.

- [ ] Wizard step navigation (Next/Back buttons with AutomationIds)
- [ ] Invest flow (amount entry + confirm via AutomationId)
- [ ] Recovery initiation (button click via AutomationId)
- [ ] Deploy confirmation (button click via AutomationId)
- [ ] Edit profile save (button click via AutomationId)

## 6. Replace Direct DI Service Calls

These bypass the UI entirely by resolving services from the container:

- [ ] `IWalletAppService` calls → drive through wallet creation UI
- [ ] `BlossomUploadService` calls → drive through upload button in edit profile
- [ ] `IProjectAppService` direct queries → drive through browse/refresh UI
- [ ] `PortfolioViewModel` direct manipulation → drive through portfolio UI

## Priority Order

1. **PointerPressed → Button.Command** (unblocks everything else)
2. **Add AutomationIds** (enables finding elements)
3. **Seed words exposure** (removes biggest reflection hack)
4. **Replace VM method calls** (uses new AutomationIds)
5. **Replace DI service calls** (final mile, lowest priority — some may stay as pragmatic bypasses)

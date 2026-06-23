# Find Projects — Invest Flow: Unplugged Actions Audit

Audit of actions exposed in the design-app Find Projects → Invest flow that are
not plugged in (or incompletely plugged in), compared against the AngorApp
`UI/Flows/InvestV2` reference flow.

Files in scope:

- `design/App/UI/Sections/FindProjects/ProjectDetailView.axaml(.cs)`
- `design/App/UI/Sections/FindProjects/InvestPageView.axaml(.cs)`
- `design/App/UI/Sections/FindProjects/InvestPageViewModel.cs`
- `design/App/UI/Sections/FindProjects/InvestModalsView.axaml(.cs)`

Reference:

- `avalonia/AngorApp/UI/Flows/InvestV2/` (PaymentSelector, Invoice, BackupWallet,
  InvestmentResult, Footer, Header).

---

## High severity — visibly broken

### 1. QR code is a fake placeholder
`InvestModalsView.axaml:333-377` renders only a static FontAwesome icon + the
caption "QR Code". There is no conversion from `OnChainAddress` /
`LightningInvoice` to a QR bitmap.

Reference: `InvoiceView.axaml:65-66` binds `Image.Source` through
`AngorConverters.StringToQRCode(SelectedInvoiceType.Address)`.

Consequence: the invoice modal cannot actually be scanned by a wallet — the
whole "pay via QR" premise of the Invoice screen is non-functional.

### 2. Liquid tab is a click-through stub
- Handler: `InvestModalsView.axaml.cs:176` → `Vm.SelectNetworkTab(Liquid)`.
- VM: `InvestPageViewModel.cs:830-833` — the `Liquid` case is an explicit
  no-op ("Stubs — no SDK call yet").
- The `InvoiceFieldLabel` binding produces `"Liquid Address"`
  (`InvestPageViewModel.cs:169`), but `InvoiceString` falls through to
  `Constants.InvoiceString = "Lightning invoices coming soon"`
  (`InvestPageViewModel.cs:269` + `Shared/Constants.cs:18`) — wrong copy for
  the Liquid tab.

### 3. Import tab is a click-through stub
Same shape as Liquid: `InvestModalsView.axaml.cs:179` → VM stub at
`InvestPageViewModel.cs:831`. Label/placeholder mismatch.

### 4. "Open in Angor Projects" button in Nostr section
`ProjectDetailView.axaml:962-976` — the `Border` has no `Name`, so
`ProjectDetailView.axaml.cs` has nothing to attach to. Expected to deep-link
to `https://angor.io/project/<id>` (or similar). Currently dead.

### 5. "Back" from the Invoice modal is missing
- VM exposes `BackToWalletSelector()` at `InvestPageViewModel.cs:780-783`.
- Nothing in `InvestModalsView.axaml` triggers it — only the X "CloseInvoice"
  button exists (`InvestModalsView.axaml:279`), which calls `CloseModal()`
  and discards the whole flow.

### 6. Invoice path ignores the user's selected wallet
- `PayViaInvoiceAsync` — `InvestPageViewModel.cs:847`:
  `var wallet = Wallets.FirstOrDefault();`
- `PayViaLightningAsync` — `InvestPageViewModel.cs:928`: same pattern.

If the user picked Wallet B in the Wallet Selector and then clicked "Or pay
an invoice instead", the receive address / Lightning swap is still generated
against whatever wallet happens to be first in the list. Should fall back to
`SelectedWallet` when set.

---

## Medium severity — inconsistent with reference / wallet flow

### 7. Fee-rate popup skipped on invoice and Lightning paths
`InvestModalsView.axaml.cs:111-128` (`PayWithWalletViaFeePopupAsync`) fires the
shared `FeeSelectionPopup` only for the **wallet** button. `PayInvoiceInstead`
jumps straight to `ShowInvoice()`, and both `PayViaInvoice` and
`PayViaLightning` use the default `SelectedFeeRate = 20` sat/vB
(`InvestPageViewModel.cs:138`) for the investment draft and for the Boltz
swap request. Either show the popup for all three paths or document why
invoice paths are fixed-fee.

### 8. No wallet-backup gate before success
The reference inserts `BackupWalletView` between the invoice monitor and the
result screen (see `PaymentSelectorViewModel.cs` L73-75 in AngorApp). In the
design app `CompleteInvestmentAfterFundingAsync`
(`InvestPageViewModel.cs:1047-1122`) goes straight from publish to
`InvestScreen.Success`. No seed-backup nudge is shown for first-time invoice
users.

### 9. Lightning swap monitor is not cancellable
`MonitorLightningSwap` at `InvestPageViewModel.cs:991-996` does **not**
receive `_invoiceMonitorCts.Token`. `MonitorAddressForFunds` on the next call
does (`:1013-1014`). Switching tabs while a Lightning swap is mid-flight
cancels the on-chain watcher but leaves the Boltz poll running to its
internal 30-minute timeout.

---

## Low severity — polish / dead UI hints

### 10. Nostr relay rows look clickable but do nothing
`ProjectDetailView.axaml:995-1007`. Both relay borders render with the brand
colour and no `Name` / handler. Users can't copy the URL. Inconsistent with
the npub/founder-key/project-id rows just above, which all have working copy
buttons.

### 11. No discrete "Payment received" visual state on the invoice modal
- VM flips `PaymentReceived = true` on funds detected
  (`InvestPageViewModel.cs:898`, `:1023`).
- XAML in `InvestModalsView.axaml` never reads the flag; feedback relies
  purely on `PaymentStatusText` changing.
- Reference `InvoiceView.axaml:28-40` toggles an explicit checkmark on
  `PaymentReceived`.

---

## Already correctly wired (for reference)

For completeness — these actions are plumbed through and behaving:

### ProjectDetailView
- Back button → `FindProjectsViewModel.CloseProjectDetail()`
- Share button → `ShareModal` via shell
- Invest / floating NavCta buttons → `FindProjectsViewModel.OpenInvestPage()`
- Explorer link → `ExplorerHelper.OpenAddress`
- Copy Project ID / Founder Key / npub buttons → `ClipboardHelper`
- Details / Nostr collapsible headers

### InvestPageView
- Back button → `FindProjectsViewModel.CloseInvestPage()`
- Submit button → `InvestPageViewModel.Submit()`
- Quick-amount borders → `SelectQuickAmount(...)`
- Subscription plan cards → `SelectSubscriptionPlan(...)`
- Copy Project ID button → `ClipboardHelper`
- `TextBox` amount → two-way bound to `InvestmentAmount`

### InvestModalsView
- Wallet card click → `SelectWallet(...)`
- PayWithWalletButton → `FeeSelectionPopup` → `PayWithWallet()`
- PayInvoiceInsteadButton → `ShowInvoice()` (kicks `PayViaInvoice` and
  switches to the On-Chain tab)
- On-Chain and Lightning tabs → `SelectNetworkTab(...)` (Lightning has full
  SDK wiring: `CreateLightningSwap` → `MonitorLightningSwap` →
  `MonitorAddressForFunds` → shared `CompleteInvestmentAfterFundingAsync`)
- CloseWalletSelector / CloseInvoice → `CloseModal()` + shell HideModal
- CopyInvoiceButton → `ClipboardHelper.CopyToClipboard(..., Vm.InvoiceString)`
- ViewInvestmentsButton → `OnNavigateBackToList` → `AddToPortfolio()` +
  `ShellViewModel.NavigateToFunded()`
- Backdrop click → `OnBackdropCloseRequested` (routes through
  `OnNavigateBackToList` on Success, else `CloseModal`)

---

## Suggested fix order

1. Real QR rendering for on-chain + Lightning (unblocks the whole Invoice
   modal as a usable feature).
2. Honour `SelectedWallet` in `PayViaInvoiceAsync` / `PayViaLightningAsync`.
3. Decide Liquid/Import: hide the tabs until they're wired, or plug them in.
   At minimum, fix the placeholder string so "Liquid Address" doesn't show
   "Lightning invoices coming soon" underneath.
4. Wire up the orphan `BackToWalletSelector()` and the "Open in Angor
   Projects" button.
5. Fee-rate popup on invoice/Lightning paths for parity with wallet path.
6. Pass `_invoiceMonitorCts.Token` to `MonitorLightningSwap`.
7. Polish: Nostr relay copy, explicit `PaymentReceived` state, backup-wallet
   prompt on the invoice path.
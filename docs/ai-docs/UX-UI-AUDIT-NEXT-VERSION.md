# UX/UI Audit — Issues to Fix for Next Version

Scope: `src/design` Avalonia app (desktop + Android). Also covers the outstanding GitHub
issue/PR queue that affects the release. Method: static analysis of ~25.4k lines of AXAML
in `UI/`, theme resources, view models, and the open issue/PR tracker.

Priority legend: **P0** = user-facing bug or money/data risk, **P1** = consistency/clarity
bug affecting many screens, **P2** = polish/robustness, **P3** = structural debt.

---

## 1. Confirmed bugs (fix first)

### P0 — Error toasts render as green success toasts
`ShellViewModel.ShowToast(message)` defaults to `ToastSeverity.Success`. Error paths call
the 1-arg overload, so failures show a **green toast with a checkmark**:

- `ManageProjectModalsView.axaml.cs:264, 302, 352, 386` — `"Claim failed: …"`, `"Release failed: …"`
- `CreateWalletModal.axaml.cs:143, 257, 263, 311, 327` — wallet create/restore/import failures
- Only `ManageProjectView.axaml.cs:209` correctly passes `ToastSeverity.Error`.

**Fix:** pass `ToastSeverity.Error` everywhere; better, delete the success-default overload
or add an analyzer/test that fails on `ShowToast($"… failed")`.
Bonus: `ex.Message` is raw SDK/internal text — map to friendly copy, keep details in logs.

### P0 — Locale-dependent amount formatting (same class of bug as the #956 claim no-op)
`AmountParser` fixed *parsing*, but *formatting* is still inconsistent within the same file.
`InvestPageViewModel.cs` mixes invariant and current-culture formatting:

- Invariant (`ToString("F8", InvariantCulture)`): lines 190, 393, 557
- Current culture ($"{…:F8}"): lines 191, 456, 458, 535, 601, 603, 618, 620

On comma-decimal locales (de-DE, fr-FR…) a release schedule mixes `0.02500000` and
`0,02500000` in the same table, and any string round-tripped through an invariant parser
silently becomes 0. Also affected: `PaymentFlowViewModel.cs:125`, `WalletInfo.cs:57,67,77,97`,
`CurrencyService.cs:28`, `PortfolioViewModel.cs:1521`.

**Fix:** introduce a single `BtcFormatter`/`MoneyDisplay` helper; rule = invariant for
round-trip strings, localised only at final display; grep-ban bare `:F8` interpolations
(add a regression test — the suite already catches this class for sweep intent).

### P0 — PR #959 (open): deploy asks for too little funding + sat/vB passed as sat/kB
Mainnet user hit a broadcast rejection; root cause confirmed via log export. Merge before
release. Stacked PR **#960** replaces hardcoded fee rates (5/20/50 sat/vB literals in
`FeeSelectionPopup`) with live indexer estimates — same transaction-cost correctness area.

### P0 — PRs #929/#930 (open): on-chain "required amount" underestimates with >1 UTXO
Funding pad budgets for 1 input / then up to 3 as a stop-gap; dynamic recheck vs actual
UTXO count is in draft (#930). Users can be asked for less than the tx actually needs.
Merge a memo of the stop-gap at minimum for this version.

### P1 — "Download Seed" writes the plaintext seed phrase to disk with no verification step
`CreateWalletModal` backup step offers **Download Seed** (plaintext file via
`SaveFilePickerAsync`) and Continue. There is:
- no "re-enter words #3/#7/#12" self-check before the backup warning clears,
- no warning that the downloaded file is *unencrypted* and must not sync to cloud/Downloads.

The shell backup-warning badge (`ShowBackupWarning`) currently trusts this flow. Note PR
#894 (encrypted seed export to Nostr+Blossom, draft) — align with it rather than investing
in plaintext export.

### P1 — No keyboard accessibility anywhere
- Zero `:focus` / `:focus-visible` styles in the V2 theme (`grep ":focus"` in
  `UI/Themes` = no hit). Custom button templates replace Avalonia defaults, so Tab focus is
  effectively invisible.
- No Escape-to-close on any modal (they're overlay panels, not windows — Avalonia gives no
  default). 19+ modal surfaces must be closed via clickable X/backdrop only.
- No TabIndex management; several icon-only buttons have no `AutomationProperties.Name`.
  (The 51 automation hooks exist only where UAT tests need them.)

**Fix (minimal, theme-level):** one global `Style Selector=":is(Control):focus-visible"` with
a 2px brand-coloured adorner; `KeyBindings`/`KeyDown` at shell level: Escape →
`CloseTopModal()`; Enter on focused primary buttons already works via Button default.

### P2 — Find Projects list never virtualizes
`FindProjectsView` renders all project cards through a custom `ResponsiveGrid : Panel`
(not a `VirtualizingPanel`). Every `ProjectCard` (image + bindings + styles) is materialised
at once — with a few hundred indexed projects this is a real mobile memory/scroll-perf hit.
Consider `VirtualizingStackPanel`/wrap-virtualizing alternative, or paginate
(already on the shared-side roadmap, see closed #654).

---

## 2. Consistency / theme-system issues (P1–P2)

### ~290 hardcoded hex colours in views bypass the token system
Semantic colours are duplicated with *different* values for the same meaning:

| Meaning | Values found | Files |
|---|---|---|
| Danger red | `#EF4444`, `#DC2626` | SettingsView (6×), FundersView (3×), ShellView badge |
| Warning/BTC orange | `#F97316`, `#F7931A`, `#FF8C00` | InvestPageView, ShellView badges ×2, ProjectDetailView |
| Success green | `#2D5A3D`, `#2d5a3d`, `#3d6448` | ProjectDetailView, InvestPageView |

Some are safe (white-on-fixed-gradient cards), but danger/warning text sits on theme-aware
surfaces and won't adapt if tokens change. **Fix:** add `Danger`, `DangerSoft`,
`Warning`, `WarningSoft` tokens to `Tokens.axaml`; mechanical regex migration; ban literal
hex outside `UI/Themes` via a layout-test-style guard.

### Typography classes exist but 99% unused
`Typography.axaml` defines `Title/Subtitle/Header/Regular/Strong`, `Size-XXS…XXL`,
`Weight-*`, `Text-*`. Adopted **18 times** vs **1,387 inline `FontSize=`** with ~19 distinct
sizes (10–80px). Fix: map current sizes onto the scale (10/11→XXS, 12→XS, 13–14→S, 16→M,
18→L, 20–24→XL, 32→XXL), migrate mechanically, delete the stray 48/80px one-offs or
promote them to named classes.

### 19 section views define their own `UserControl.Styles`
Local template overrides (hover states etc.) duplicated outside `UI/Themes` — high drift
risk shell already shows this (6 duplicated template overrides just for shell bar buttons).
Fix: promote genuinely shared patterns into `Buttons.axaml`; keep only true one-offs local.

### Truncated identifiers without reveal affordance
36 `TextTrimming="CharacterEllipsis"` truncations, only 20 tooltips *total* in the app.
Ellipsised project IDs / npubs / txids with no tooltip = dead-end information. Fix:
`ToolTip.Tip="{Binding <same property>}"` on truncated ID text, or a shared
`CopyableId` control (elide-middle + click-to-copy + toast). `ClipboardHelper` already
centralises the toast.

### Manual `Opacity=0.35` "disabled" buttons instead of `IsEnabled`
e.g. `InvestPageViewModel.SubmitOpacity`. Opacity-only disabling keeps the button clickable
and skips hover/pressed suppression; also invisible to a11y. Fix: bind `CanSubmit` to
`IsEnabled` and add one theme-level `:disabled` style.

---

## 3. UX flow issues (P2)

### Wallet seed/backup flow
- Import textarea accepts seed with **no word-count client-side hint** until submit
  (`twelve or twenty-four seed words…` only as watermark); show live validation
  (12/24 words + BIP-39 wordlist check) under the field.
- Two spend-password boxes (`PasswordChar="*"`) have **no reveal toggle** — typo-prone on
  mobile (report relates to closed #817 Branta send-side verification flow).

### Wallet creation is one undifferentiated steep step
"Create / Import / Restore-backup" all live behind one modal; consider splitting entry
choice into 3 cards (also simplifies the `BtnContinueBackup` `IsEnabled="False"` initial
state logic).

### Wipe Data confirm could be stronger for the wallet-bearing case
Good: explicit modal, reset checkbox defaults, destructive-coloured button. Improve: when
a wallet *without* verified backup exists, require typing `WIPE` (or wallet name) —
prevents muscle-memory destruction. Title/button use two different danger reds (see §2).

### Toast discipline
Single global toast is good (severity + dismiss + auto-clear). Add: max-width + wrap stays
readable on phones, queue/stacking for >1 concurrent notification (fundamentals are in
`ShellViewModel`), and keep duration proportional to message length.

### Invest page penalty-threshold messaging
`ThresholdStatusText` flips between "Requires Approval" / "No Approval Needed" as the user
types — verify there is no layout jump; and the default when a project has **no** threshold
is "Requires Approval" (line ~213 comment: `return true`). Confirm that wording is intended
— it will alarm users on every no-threshold project.

---

## 4. What NOT to re-flag (already fixed on this branch / main)

- Android back button, mobile inconsistencies of #920 / #950 — merged.
- Comma-decimal claim no-op (#956) — fixed via `AmountParser` (but see §1 formatting debt).
- "Loading project…" infinite hang from EOSE tracking (#954), indexer-failure noise (#953).
- Dark-mode icon bake + regression tests (#949/`2f881a78`).
- No custom mouse-wheel handlers exist — nothing mouse-first to fix; Avalonia ScrollViewers
  handle touchpad natively.
- Wipe-data, backdrop-close-on-modals, backup badge click-through — all present.

---

## 5. Repository hygiene (open issues/PRs for the release)

**Merge/land before next version (functional, money-path):**
- PR **#959** deploy funding amount + sat/kB unit fix *(P0)*
- PR **#960** live fee rates from indexer *(P0, stacked on #959)*
- PR **#929/#930** on-chain required-amount UTXO padding/recheck *(P0/P1)*
- PR **#942** founder scan resilience + derivation fixes *(P1 — affects founder claim UX)*

**Triage the open issue list (
most are stale or non-UX):**
- #838 NBitcoin workaround removal — scheduled for the dependency upgrade; not user-facing.
- #692 macOS dmg "damaged or incomplete" on M4 — release notarisation/signing; the
  SignPath Windows signing (#940) landed, macOS story presumably needs the same treatment.
  This is a *first-launch blocker for every macOS user* and the only real install bug open.
- #349 SoB'25 competency-test submission — close (not an issue).
- #247 NIP4 → NIP17/NIP44/NIP59 DM encryption — security debt: investor↔founder messages
  use a deprecated NIP. Schedule deliberately; it's protocol-touching.
- #113 proof-of-investment nostr post — enhancement, keep.

---

## 6. Suggested ordering for the release branch

1. **#959 + #960 + #929/#930** (money-path correctness).
2. P0 toast-severity bug (10-line fix + guard).
3. P0 `BtcFormatter` unification + regression test (comma-locale test already has precedent).
4. Keyboard pass: `:focus-visible` style + Escape-to-close + Enter handling on modals
   (one shell-level service, biggest a11y win per line changed).
5. Colour-token migration (danger/warning first; mechanical).
6. Backup flow hardening (verification step + plaintext-download warning; coordinate with
   #894 direction).
7. Find Projects virtualisation (only if project counts justify; otherwise P2 → backlog).
8. Typography-class migration (mechanical, batch, low risk with layout-regression tests).
9. Issue-tracker cleanup per §5.

Every view change should re-run:
`dotnet test src/design/App.Test.Integration --filter FullyQualifiedName~LayoutRegression`
(the suite exists precisely for this kind of mechanical sweep).

# Handoff — further-ux-polish branch

Context for the next agent. Read first, then the audit: `docs/ai-docs/UX-UI-AUDIT-NEXT-VERSION.md`.

## Branch state (all committed, working tree clean)

Branch `further-ux-polish`, based on latest `origin/main` (75d5595e) + cherry-picked
Linux-dev commit. Old `ux-ui-pass` was squash-merged to main via PR #955; nothing local
is missing from main except what this branch adds.

| Commit | Content |
|---|---|
| `d02b8ab6` | chore(dev): Linux dev support (cherry-picked; needed for APK builds on this machine) |
| `385b8020` | docs: the UX/UI audit report (scope doc for this whole effort) |
| `8702c887` | feat(ux): the 6 workstreams below |
| `60575de0` | fix(theme): focus ring template type |

## Scope agreed with the user

Fix **bug #1** (toast severity) + **UX items #4–#10** from the audit summary only.
Explicitly OUT of scope: locale formatting (#2), money-path PRs #959/#960/#929/#930/#942
(they're not the backend owners), repo hygiene. After the code work the user wants:
**build the APK + a manual test guide, then a deeper UX/UI scan.**

## What's done (commit 8702c887)

1. **Toast severity** — `ToastRequested` events in SettingsViewModel, FundersViewModel,
   EditProfileViewModel, ManageProjectViewModel are now `Action<string, ToastSeverity>`;
   4 view handlers pass severity through; all failure call sites pass
   `ToastSeverity.Error` (warnings use `Warning`). Portfolio/MyProjects already had the
   correct pattern — used as the template.
2. **Keyboard** — new `UI/Themes/V2/Styles/Keyboard.axaml` (registered in Theme.axaml):
   global `:focus-visible` ring on interactive controls, accessible names on
   `ModalCloseBtnSmall`. Escape handling: tunnel `KeyDown` at ShellView root →
   `vm.TryHandlePlatformBack()` (closes modal / backs out of detail views), plus
   SettingsView local modals (network + wipe).
3. **Color tokens** — new root (theme-invariant) brushes in Colors.Core.axaml:
   `DangerBrush` #DC2626, `DangerBrightBrush` #EF4444, `DangerSoftBrush` #1ADC2626,
   `WarningBrush` #F97316, `WarningBrightBrush` #FF8C00, `WarningSoftBrush` #30FF8C00,
   `BitcoinAccentBrush` #F7931A. 50 literal usages migrated across views via regex.
4. **Tooltips** — 31 truncated bound TextBlocks got `ToolTip.Tip` mirroring their binding.
5. **Passwords/seed** — eye-reveal toggles (PrivateKeysPasswordModal + claim/release
   password fields in ManageProjectModalsView), plaintext-download warning in
   CreateWalletModal, live 12/24 word-count hint with theme-aware Valid/Invalid classes.
6. **Typography** — 938 TextBlock/SelectableTextBlock `FontSize="N"` attributes migrated
   to classes. On-scale→named (11→Size-XXS, 12→Size-XS, 14→Size-S, 16→Size-M, 18→Size-L,
   25→Size-XL, 32→Size-XXL); off-scale got interim exact-pixel classes
   (Size-10/13/15/20/22/24/26/28/30/36/48/80) — marked in Typography.axaml as legacy
   pending a deliberate visual consolidation (13→S, 15→M, 20-24→XL would change rendering).
   i:Icon/Button/TextBox FontSize intentionally untouched.
7. **Dead code** — removed `InvestPageViewModel.SubmitOpacity` (buttons already use
   real `IsEnabled={Binding CanSubmit}`; global `Button:disabled` style exists in Buttons.axaml).

**Test state: all 187 LayoutRegression tests PASS** (`/tmp/layout-tests-further-ux-2.log`).
Baseline before the branch was also 187/187.

## What's NOT done (next tasks, in order)

1. **Item 9: Find Projects virtualization** — `ResponsiveGrid : Panel` is
   non-virtualizing; every ProjectCard materializes at once. Options: (a) implement a
   virtualizing wrap/responsive panel (heavy), (b) incremental reveal / "load more"
   (simple, real perf win, small UX change — discuss), (c) decide project counts don't
   justify it and document. Skipped pending decision.
2. **Optional regression guard**: a source-scan xUnit test asserting the migrated literal
   hexes (`#DC2626|#EF4444|#F7931A|#F97316|#FF8C00|#30FF8C00|#1ADC2626`) never reappear in
   `UI/Sections|Shell|Shared`. No source-scan harness exists yet (DarkModeIconTests is
   runtime-rendering based) — walk up from AppContext.BaseDirectory to find `src/design/App/UI`.
3. **Build the APK** for the user (see Environment gotchas), write the manual test guide.
4. **Deeper UX/UI scan** after the above (user asked for it explicitly).

## Environment gotchas (this Linux machine)

- **NuGet**: every dotnet command needs `DOTNET_NUGET_SIGNATURE_VERIFICATION=false`
  (NU3012 revoked-certificate false positives on Linux). No workaround—just set it.
- Layout regression suite (fast, authoritative for view changes):
  ```
  dotnet build src/design/App.Test.Integration/App.Test.Integration.csproj
  dotnet test src/design/App.Test.Integration/App.Test.Integration.csproj \
    --filter "FullyQualifiedName~LayoutRegression" 2>&1 | tee /tmp/layout.log
  ```
  ~2–3 min. ALWAYS pipe to a file (per AGENTS.md).
- APK build/runner: `./scripts/dev-run.sh` (builds APK + installs to USB device + runs
  desktop). Needs JDK17 + dotnet android workload. `JAVA_HOME` detection is built in.
- Desktop run: `dotnet run --project src/design/App.Desktop -c Debug`.

## Hard-won pitfalls (do not repeat)

- **`FocusAdorner` expects `<FocusAdornerTemplate>`, NOT `<ControlTemplate>`** —
  assigning ControlTemplate builds fine but throws InvalidCastException at render
  (caught by the layout tests, 178/187 failed; fixed in 60575de0).
- **`TextBox.PasswordChar` is non-nullable `char`** — reveal by setting `'\0'`, not null
  (CS0037 otherwise).
- **Python regex over XAML tags**: `[^>]*` / `>(?!/)` alternation tricks are unsafe —
  `(?:[^>]|>(?!/))*` silently consumes to EOF. Use lazy `([^>]*?)(/>|>)` and
  XML-parse every rewritten file before writing (the good script did; the bad one didn't).
- **NEVER `git checkout -- <directory>` to revert** — it nukes .cs along with .axaml.
  That mistake wiped all .cs fixes mid-session; everything was re-derived deterministically
  and re-committed. Revert by explicit file list only.
- `dotnet test` without explicit `--no-build --no-restore` on the integration project can
  stall without producing output — always build first, test with `--no-build`.
- The theme replaces default templates → any global style assumption from Fluent
  (focus visuals, borders) must be re-verified against the actual theme files.

## Verification cheat-sheet for the user's manual APK test

New things to look at: (1) failure toasts are now red not green (e.g. reject signature in
Funders); (2) Esc closes modals on desktop; (3) focus ring visible when tabbing through
forms; (4) truncated IDs show full value on hover; (5) password eye icons in founder
modals + view-keys flow; (6) create-wallet import shows live word count, backup step shows
amber "not encrypted" warning; (7) danger/warning colours unchanged visually (token swap).

# FindProjectsPanelTests

## Purpose

Tests the Find Projects section's panel visibility state machine and project display. The section has 3 mutually exclusive panels (ProjectListPanel, ProjectDetailPanel, InvestPagePanel). Tests are consolidated into flow tests to avoid booting separate app instances.

## Tests

### `FindProjectsFlow_NoWallet_PanelStatesAndProjectDisplay`

| | |
|---|---|
| **Type** | Integration |
| **Network** | Testnet (project loading from relay) |
| **Duration** | ~30s |

**Verifies**: Panel transitions (list → detail → invest → list), project card rendering and field binding, statistics loading, detail view metadata, type terminology (Fund vs Invest), invest button visibility, HasInvested flag, invest form states (initial, quick amount, manual amount, submit validation), reload, and navigate-away/back state reset.

**Steps**:
1. Navigate to Find Projects — verify list panel visible, others hidden.
2. Wait for projects to load from SDK.
3. Verify ProjectCard controls render with correct fields.
4. Open project detail — verify panel transition.
5. Check detail metadata, type terminology, invest button visibility.
6. Close detail — verify return to list.
7. Open invest page — verify InvestForm initial state and amount validation.
8. Reload projects and navigate away/back — verify state reset.

---

### `FindProjectsFlow_WithWallet_WalletSelectorAndInvoice`

| | |
|---|---|
| **Type** | Integration |
| **Network** | Testnet |
| **Duration** | ~45s |

**Verifies**: With a wallet created, the invest flow shows a wallet selector, displays wallet name and balance, handles wallet selection state, shows insufficient balance errors, and supports invoice screen toggling and modal close reset.

**Steps**:
1. Create wallet via UI.
2. Navigate to Find Projects, open a project, open invest page.
3. Submit amount — verify wallet selector appears.
4. Check wallet display and selection state.
5. Attempt payment with insufficient balance — verify error.
6. Test invoice screen toggle and close modal reset.

# InvestModalsViewFixesTest

## Purpose

Fast smoke test verifying recent fixes to the InvestPageViewModel and InvestModalsView. Tests that error bindings, immediate UX feedback, tab-driven labels, and defensive error labeling all work correctly without needing a funded wallet or testnet faucet.

## Tests

### `InvestModals_FixesAreWired`

| | |
|---|---|
| **Type** | Integration (smoke) |
| **Network** | None (errors expected — tests the error paths) |
| **Duration** | ~5s |

**Verifies**:
1. `HasError` binding drives the error banner (flips with `ErrorMessage`).
2. `ShowInvoice()` defaults to on-chain tab with immediate `IsProcessing` and `PaymentStatusText`.
3. `InvoiceString` follows live `PaymentStatusText` (not a static placeholder).
4. `SelectNetworkTab(Lightning)` synchronously sets `IsGeneratingLightningInvoice` and Lightning status text.
5. `InvoiceFieldLabel` and `InvoiceTabIcon` follow the active tab.
6. Without wallets, errors are labeled (e.g., "No wallet available") not raw exceptions.

**Steps**:
1. Navigate to Find Projects, open a project, and open the invest page via UI navigation.
2. Test HasError ↔ ErrorMessage binding round-trip.
3. Set amount, call ShowInvoice, verify synchronous on-chain state.
4. Wait for async on-chain flow — verify labeled error.
5. Switch to Lightning tab — verify synchronous Lightning state.
6. Wait for async Lightning flow — verify labeled error.
7. Switch to Liquid and Import stub tabs — verify labels and no crash.

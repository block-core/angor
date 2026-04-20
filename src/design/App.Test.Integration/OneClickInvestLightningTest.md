# OneClickInvestLightningTest

## Purpose

Tests the Lightning invoice tab state machine in the 1-click invest flow. Verifies Boltz swap creation, tab switching isolation (Lightning errors don't bleed from on-chain), stub tab safety, and modal reset. Also tests portfolio deduplication after an investment is added.

## Tests

### `LightningInvoiceFlow_FullStateMachine`

| | |
|---|---|
| **Type** | Integration |
| **Network** | Testnet (Boltz swap attempt) |
| **Duration** | ~15s |

**Verifies**: The complete Lightning tab state machine — from setting an amount through invoice generation (or labeled error), tab switching without error bleed-through, stub tabs (Liquid/Import), and CloseModal full reset.

**Steps**:
1. Navigate to Find Projects, open a project, open the invest page via the UI navigation flow.
2. Set investment amount to 0.001 BTC and call `ShowInvoice()`.
3. Switch to Lightning tab — verify `IsGeneratingLightningInvoice` flips immediately.
4. Wait for Lightning flow to complete (invoice or error).
5. Verify on-chain monitoring errors don't bleed into Lightning tab.
6. Round-trip between On-Chain and Lightning tabs.
7. Switch to Liquid and Import stub tabs — verify no crash.
8. CloseModal — verify all state is reset.

---

### `PortfolioDeduplication_AfterInvestment`

| | |
|---|---|
| **Type** | Integration |
| **Network** | None |
| **Duration** | < 2s |

**Verifies**: Adding the same project investment twice via `AddInvestmentFromProject` does not create a duplicate entry in the portfolio.

**Steps**:
1. Resolve PortfolioViewModel from DI.
2. Add an investment for a test project — assert count increases by 1.
3. Add the same investment again — assert count stays the same (deduplication).

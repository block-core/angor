# OneClickInvestOnChainTest

## Purpose

Tests the on-chain invoice tab state machine in the 1-click invest flow. Verifies address generation, payment monitoring, tab switching error isolation (the bug where cancelled on-chain monitoring leaked errors into Lightning), and rapid tab-switch race conditions.

## Tests

### `OnChainInvoiceFlow_FullStateMachine`

| | |
|---|---|
| **Type** | Integration |
| **Network** | Testnet (address generation attempt) |
| **Duration** | ~10s |

**Verifies**: The complete on-chain tab state machine — ShowInvoice defaults to on-chain, address generation starts, labeled errors surface (not raw exceptions), tab switching clears on-chain errors, and CloseModal resets all state.

**Steps**:
1. Navigate to Find Projects, open a project, open the invest page via UI navigation.
2. Set investment amount and call `ShowInvoice()`.
3. Verify on-chain is the default tab with immediate processing feedback.
4. Wait for async flow to complete (address or error).
5. Switch to Lightning — verify on-chain monitoring error doesn't persist.
6. Switch back to on-chain — verify fresh flow starts.
7. CloseModal — verify full state reset.

---

### `OnChainInvoiceFlow_TabSwitchRaceCondition`

| | |
|---|---|
| **Type** | Integration |
| **Network** | Testnet |
| **Duration** | ~10s |

**Verifies**: Rapidly switching between on-chain and Lightning tabs does not cause stale monitoring errors to leak through.

**Steps**:
1. Start on-chain monitoring via ShowInvoice.
2. Rapidly switch: Lightning → OnChain → Lightning.
3. Wait for cancelled operations to settle.
4. Verify no stale "monitoring has stopped" errors appear.

# SendToSelfTest

## Purpose

Full end-to-end integration test that boots the real Avalonia app in headless mode, creates a wallet from scratch, funds it via a testnet faucet, and sends bitcoin to itself — verifying the entire wallet lifecycle through the UI.

## Prerequisites

- Internet connectivity (testnet indexer + faucet API)
- Profile isolation: uses `test-send-receive` profile (`~/.local/share/App/Profiles/test-send-receive/`)
- Testnet faucet must be operational at `https://faucettmp.angor.io`

## Tests

### `FullSendToSelfFlow`

| | |
|---|---|
| **Type** | End-to-end integration |
| **Network** | Signet testnet (real indexer + faucet) |
| **Duration** | 30–120s |

**Verifies**: The complete wallet lifecycle works through the real UI — wallet generation, funding, receiving an address, sending a transaction, and confirming the balance.

**Steps**:

1. **Boot app** — Create a headless `Window` containing the full `ShellView` with real DI services.
2. **Wipe data** — Navigate to Settings and call `ConfirmWipeData()` to delete any wallets/settings from a previous run.
3. **Verify empty state** — Navigate to Funds section, assert the `EmptyStatePanel` is visible.
4. **Create wallet (Generate path)** — Click "Add Wallet" → click "Generate New" → click "Download Seed" (no-op in headless, `NoopStorageProvider` returns null) → click "Continue" → wait for `CreateWalletSuccessPanel` → click "Done".
5. **Wait for WalletCard** — Poll until the `WalletCardBtnSend` button appears (proves the populated state rendered).
6. **Request faucet coins** — Click the `WalletCardBtnFaucet` button, wait 5s for the HTTP request.
7. **Wait for non-zero balance** — Poll-click `WalletCardBtnRefresh` every 5s until `TotalBalance != "0.0000"` (up to 120s timeout).
8. **Get receive address** — Click `WalletCardBtnReceive`, poll-wait for `ReceiveAddressText` to change from `"Loading..."` to a `tb1...` address, close the modal.
9. **Send to self** — Set `SimplePasswordProvider` key to `"default-key"`, click `WalletCardBtnSend`, fill `SendAddressInput` with the receive address, fill `SendAmountInput` with `"0.00010000"`, click `BtnSendConfirm`, wait for `SendSuccessPanel`, read `SummaryTxid`, click `BtnSendDone`.
10. **Verify final balance** — Click refresh, assert `TotalBalance > 0` (balance minus fee).

**Key implementation details**:

- All control lookups use `AutomationProperties.AutomationId` (portable to Appium).
- WalletCard buttons have dual identifiers: `Name` (for code-behind routing) and `AutomationId` (for tests).
- The Generate wallet flow works in headless because `NoopStorageProvider.SaveFilePickerAsync()` returns `null` without throwing, and `_seedDownloaded` is set to `true` regardless.
- `SimplePasswordProvider` must be set to `"default-key"` before the Send step — this matches the encryption key used by `CreateWalletModal` when creating the wallet.

**How to run**:
```bash
dotnet test src/design/App.Test.Integration/App.Test.Integration.csproj \
  --filter "DisplayName~FullSendToSelfFlow" \
  --logger "console;verbosity=detailed"
```

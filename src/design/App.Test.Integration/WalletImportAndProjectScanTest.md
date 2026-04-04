# WalletImportAndProjectScanTest

## Purpose

Full end-to-end integration test for **wallet import from seed words** and the **ScanFounderProjects** flow. This validates the most critical recovery path a user needs after device loss: importing a seed phrase on a new device and rediscovering their founder projects.

## Profiles

- Creator profile: `WalletImportScan-Creator`
- Importer profile: `WalletImportScan-Importer`

All profiles are created under the application's profile directory.

## Scenario

1. Creator profile generates a new wallet, captures the seed words from the backup screen, and funds the wallet via the signet faucet.
2. Creator deploys a **Fund** project with a single stage, recording the project identifier.
3. Importer profile imports the same seed words via the wallet Import flow (ChoicePanel -> BtnImport -> SeedPhraseInput -> BtnSubmitImport).
4. The test verifies that the imported wallet produces the same WalletId as the original (deterministic derivation from xpub hash).
5. Importer refreshes balance and verifies the same funds are visible.
6. Importer runs ScanForProjectsAsync to rediscover founder projects from the blockchain.
7. The test asserts the originally-deployed project appears in the importer's MyProjects list.

## Key Validations

- Wallet import UI flow completes successfully through all panels (Choice -> Import -> Success).
- Imported wallet WalletId matches the originally-generated wallet (same seed = same identity).
- Balance is preserved across import (same UTXOs visible).
- ScanFounderProjects discovers the project deployed by the same seed/key on a different profile.
- Project identifier matches the originally-deployed project.

## Notes

- The test reuses the real headless Avalonia app and switches profiles by rebuilding DI per profile.
- Seed words are captured from the `SeedPhraseDisplay` TextBlock during the Generate flow's BackupPanel.
- The wallet import path uses name-based TextBox lookup for `SeedPhraseInput` since the TextBox is identified by Name rather than AutomationId.
- ScanForProjectsAsync iterates all 15 derived key slots against the indexer, so it requires the project creation transaction to be indexed.

## Run

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~WalletImportAndProjectScanTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Integration/App.Test.Integration.csproj" --filter "FullyQualifiedName~WalletImportAndProjectScanTest" --logger "console;verbosity=detailed"
```

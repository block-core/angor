# Security Analysis: Angor.Shared.Protocol + Wallet/Derivation

Scope: `shared/Angor.Shared/Protocol/**`, `DerivationOperations.cs`, `IDerivationOperations.cs`, `ProjectIdentifierDerivation.cs`, `WalletOperations.cs`, `IWalletOperations.cs`, `Models/Wallet.cs`, `Models/WalletWords.cs`.

## Critical

### C1. Schnorr signature + sighash logged at Information level
- `shared/Angor.Shared/Protocol/FounderTransactionActions.cs:74`
- `shared/Angor.Shared/Protocol/FounderTransactionActions.cs:173`
- `shared/Angor.Shared/Protocol/InvestorTransactionActions.cs:313`
- `shared/Angor.Shared/Protocol/InvestorTransactionActions.cs:359`
- `shared/Angor.Shared/Protocol/SeederTransactionActions.cs:93`

Logging `(pubkey, sighash, signature)` builds a perfect corpus for k-reuse key recovery if BIP-340 determinism ever regresses (library bug, mocked RNG, environment anomaly). **Remediation:** remove signatures and sighashes from Information-level logs.

### C2. Mnemonic + passphrase as unprotected `string`, serialized to JSON, never zeroized
- `shared/Angor.Shared/Models/WalletWords.cs:10-21`
- `shared/Angor.Shared/Models/Wallet.cs`
- `shared/Angor.Shared/WalletOperations.cs:411,454`
- `shared/Angor.Shared/DerivationOperations.cs:31,183`

Seed + passphrase live as mutable CLR strings, expanded to seed on every derivation; GC keeps copies resident. **Remediation:** pinned buffers, `CryptographicOperations.ZeroMemory`, derive `ExtKey` once per session, never persist plaintext `WalletWords`.

### C3. Private keys passed as hex `string` across signing APIs
- `shared/Angor.Shared/Protocol/FounderTransactionActions.cs:41,86,93`
- `shared/Angor.Shared/Protocol/InvestorTransactionActions.cs:99`
- `shared/Angor.Shared/Protocol/SeederTransactionActions.cs:62-63,98`
- `shared/Angor.Shared/Protocol/TransactionBuilders/SpendingTransactionBuilder.cs:87`

Same exposure surface as C2, multiplied across every signing call site. **Remediation:** accept `Key`/`ECPrivKey` in `using` blocks.

## High

### H1. Project identifier truncated to 31 bits → BIP-32 collision
- `shared/Angor.Shared/DerivationOperations.cs:230-247`

`upi = hashOfid.GetLow64() & int.MaxValue;` → birthday collision ~46k projects, targeted grind ~2³¹. Same `upi` is used for founder recovery, investor, Nostr, and secret-hash paths → cross-project key reuse + signature replay. **Remediation:** use two hardened levels, or full 256-bit content addressing distinct from the derivation index.

### H2. Coins/keys joined by list position
- `shared/Angor.Shared/WalletOperations.cs:87,216,279`

Parallel-list indexing lets attacker-controlled `UtxoDataWithPath` ordering cause signing under the wrong key. **Remediation:** return `List<(Coin, Key)>` tuples; validate `key.PubKey.WitHash.ScriptPubKey == coin.ScriptPubKey`.

### H3. No `ScriptPubKey` ↔ derived key validation before signing
- `shared/Angor.Shared/WalletOperations.cs:90,218,282`

`SignInput(network, key, coin, SigHash.All)` called without verifying the coin's scriptPubKey is controlled by `key`. Combined with H2, enables sign-of-attacker-chosen-message via compromised indexer. **Remediation:** enforce strict match check.

### H4. Non-BIP-341 unspendable internal Taproot key
- `shared/Angor.Shared/Protocol/Scripts/TaprootScriptBuilder.cs:119-138`

Uses `SHA256("Angor Unspendable Taproot Key")` instead of standard NUMS point `0x50929b74…`. If any party knows the discrete log, they sweep every investment UTXO via key path, bypassing all script-path protections. **Remediation:** use the standard BIP-341 NUMS point (as the commented-out line suggested).

### H5. `DeriveLeadInvestorSecretHash` hashes raw private key bytes as public commitment
- `shared/Angor.Shared/DerivationOperations.cs:96`

Couples a published commitment to a sibling-derived signing key. **Remediation:** dedicated derivation branch for preimage.

## Medium

- **M1.** `new Key(secretBytes)` wrapping preimages — `shared/Angor.Shared/Protocol/Scripts/TaprootScriptBuilder.cs:33,41`, `shared/Angor.Shared/Protocol/SeederTransactionActions.cs:98`.
- **M2.** `Sequence = LockTime.Value` accidentally relies on year-2038 high bit to disable BIP-68 — `shared/Angor.Shared/Protocol/FounderTransactionActions.cs:121,299`, `shared/Angor.Shared/Protocol/TransactionBuilders/SpendingTransactionBuilder.cs:46,66`. Use `0xFFFFFFFE`.
- **M3.** Witness reconstructed by positional `Skip/Take` without schema check — `shared/Angor.Shared/Protocol/InvestorTransactionActions.cs:209-229`.
- **M4.** `CheckRecoverySignatures` exists but `AddSignaturesToRecoveryPathTransaction` never calls it — `shared/Angor.Shared/Protocol/InvestorTransactionActions.cs:283-367`.
- **M5.** `Debug.Assert` used to verify own signature — compiled out in Release — `shared/Angor.Shared/Protocol/FounderTransactionActions.cs:176`.
- **M6.** `SeederScriptTreeBuilder.CreateThresholds` unbounded `C(n,k)` leaf explosion → OOM/DoS — `shared/Angor.Shared/Protocol/Scripts/SeederScriptTreeBuilder.cs:40-66`.
- **M7.** Platform-endian `BitConverter.GetBytes(keyType)` — `shared/Angor.Shared/Protocol/Scripts/ProjectScriptsBuilder.cs:47`.
- **M8.** `GetInvestmentDataFromOpReturnScript` distinguishes variants by push length only — `shared/Angor.Shared/Protocol/Scripts/ProjectScriptsBuilder.cs:73-118`. Add 1-byte version discriminator.

## Low / Informational

- **L1.** `Hash256(privateKey)` as Nostr storage password — `shared/Angor.Shared/DerivationOperations.cs:267-279`. Prefer HKDF with label.
- **L2.** Huffman tree weights (70/40/1/10) unversioned — `shared/Angor.Shared/Protocol/Scripts/TaprootScriptBuilder.cs:86-100`. Weight changes soft-brick funded UTXOs.
- **L3.** Network read per-call from mutable `INetworkConfiguration` → cross-network confusion risk.
- **L4.** Mnemonic RNG depends on NBitcoin defaults — `shared/Angor.Shared/WalletOperations.cs:33-39`.
- **L5.** Misleading "Keys derivation limit exceeded" exception — `shared/Angor.Shared/DerivationOperations.cs:77`.
- **L6.** `BuildUpfrontUnfundedReleaseFundsTransaction` accepts address-or-hex ambiguous union — `shared/Angor.Shared/Protocol/TransactionBuilders/InvestmentTransactionBuilder.cs:117-141`.
- **Info.** `ProjectIdentifierDerivation.cs:17-37` scalar add without mod-`n`. `ExtKey.UseBCForHMACSHA512 = true` toggled as global static (thread-unsafe) at `DerivationOperations.cs:232-233`. Hard-coded 294-sat dust threshold in `WalletOperations.cs:198,264`. Redundant `upi >= 2_147_483_648` check after mask at `DerivationOperations.cs:243`.

## What Looks Good

- `SignTaprootKeySpend` via NBitcoin (BIP-340 deterministic) — no homebrew Schnorr.
- Sighash flags fixed and intentional (`Single|AnyoneCanPay` for cosigned recovery paths).
- Stage-output sum invariant asserted (`InvestmentTransactionBuilder.cs:93`).
- CLTV + `LockTime` + enabled sequence are coherent.
- Threshold seeder script correctly emits `OP_HASH256 … OP_EQUALVERIFY` per preimage.
- `GetTaprootFullPubKey()` used consistently, avoiding 33-vs-32-byte x-only confusion.

## Top Four Before Mainnet Scaling

1. **H4** — switch to standard BIP-341 NUMS point.
2. **C1** — strip signatures from logs.
3. **H1** — widen project-identifier derivation beyond 31 bits.
4. **C2/C3** — eliminate plaintext string handling of seeds and private keys.
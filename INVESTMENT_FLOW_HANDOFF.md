# Investment flow handoff — 22 September 2026

Read this file and each repository's AGENTS.md before resuming. This handoff is saved in both repositories. The work is not yet fully PR-ready.

## Repositories and branch

Both checkouts use **blazer-app-improvements** (spelling intentional):

| Repository | Local checkout | Implementation commit |
| --- | --- | --- |
| block-core/angor-hub | /home/yaya/Projects/angor-hub | 8e65ead0122d3de082471cf279ea16554b4df400 |
| block-core/angor | /home/yaya/Projects/angor-blazor | accab080f37e8d460a592f61c5a374011943a9bb |

The user authorized committing and pushing the current changes to these existing branches, plus saving context. No PR creation, merge, or deployment was requested. This documentation follows the implementation commits; inspect `git log` and remote tracking before further work.

## User intent and UI preferences

Make mainnet and testnet projects load reliably, preserve the network/indexer between Hub project information and Blazor funding, and allow testing the full investment flow before a PR. Pending investment statistics must animate instead of briefly displaying zero. Copying an on-chain address or Lightning invoice must show the existing inline tick, without a toast.

Funding layout must match the desktop app: amount card above a separate funding-pattern card in the left column, schedule in the middle, transaction details on the right. Patterns are full-width selectable rows showing name, stage count, frequency and description. **Keep all four amount presets in one row** (the user's final correction). Mobile stacks the cards.

## Implemented

### Hub

- Mainnet primary indexer is https://indexer.angor.io/; testnet is https://test.indexer.angor.io/. Retired explorer configuration migrates to the working mainnet indexer while preserving custom settings.
- Shared requests time out and fall back within the same network. Concurrent failures share a fallback probe. Investor/address requests use the actual active indexer, not the saved primary.
- A failed project validation no longer discards other verified results. Pagination can retry failures; partial transaction results are not cached as complete.
- Payment navigation passes the active `indexer` plus `network=Main` or `network=Angornet` to Blazor.
- Shared stat placeholder pulses while pending, respects reduced motion, and shows an em dash on failure. Actual confirmed zero remains zero. Detail statistics wait for the appropriate full data; percentage calculations avoid NaN.
- `npm start` launches both apps with WASM runtime optimizations enabled. `ANGOR_BLAZOR_DEBUG=1 npm start` restores managed debugging, which makes cryptography substantially slower. README documents this.

### Blazor / shared .NET

- Apply the handoff network before the handoff indexer. Switch automatically only when no wallet exists; preserve an existing wallet and explain mismatched links.
- Background network health checks merge only health results into current settings, instead of overwriting a newly selected indexer/network with a stale snapshot.
- Relay connection tracking waits for initial connecting relays during EOSE monitoring and excludes failed ones. This change, and the initial seed-copy component work, were already present when the later fixes began and are preserved in the implementation commit.
- Browser wallet preparation uses asynchronous Web Crypto BIP-39 PBKDF2 and primes the existing NBitcoin extended-key cache. English mnemonics avoid loading all language word lists. Password unlock primes the same cache; signing reuses it.
- Yield between browser address derivations and major preparation steps. Temporary investor wallets skip unused founder-key batches. Seed/key serialization remains compatible; regression tests cover vectors, caching and disposal.
- Shared `CopyButton.razor` displays the copied tick and inline retry state. Seed-copy wrapper and both invoice copy buttons reuse it.
- Funding-pattern cards restored, amount presets remain one row, and long project IDs wrap without overflowing Firefox/WebKit phone layouts.

## Running and testing

From the Hub checkout, run `npm start` if the servers are not already running:

- Hub: http://localhost:4200
- Blazor: http://localhost:5062
- Local .NET 8 executable: `~/.local/share/angor-dotnet/dotnet`
- Dev server log at handoff: `/tmp/angor-dev-optimized.log`. Both servers were left running. Avoid duplicate servers or stopping them just for test cleanup.

Useful live projects (availability/dates may change):

- Testnet investment: http://localhost:4200/project/angor1qj0fgaszgsdv8yzlww0x8k5gen2403ew8e3v8jh?network=test (`debug-invest-0.2.35-4`). Hub → Blazor → web option → recovery backup → Bitcoin invoice was exercised with disposable, unfunded wallets.
- Mainnet funding-pattern reference: http://localhost:4200/project/angor1qde7y830vtajref5vwag5x459m9w5y75gd49v4t?network=main (Venezuela relief, 3 Weekly / 6 Weekly).
- Mainnet Casa Bitcoin: `angor1qryhse38vcyqnp0j6976q9f00a9jpj2ary03nlc`.

Read `~/.codex/skills/browser-check/SKILL.md` for browser validation. Keep its scenarios and artifacts outside repositories. Local scenarios are in `~/.local/share/browser-check/projects/angor-hub/`, notably `pattern-layout.mjs`, `wallet-final.mjs`, `testnet-payment.mjs`, `payment-flow.mjs`, `stat-loading.mjs`, and `stat-error.mjs`.

Example:

```bash
~/.local/bin/browser-check --base=http://localhost:4200 --scenario="$HOME/.local/share/browser-check/projects/angor-hub/wallet-final.mjs" --engines=chromium --profiles=desktop --out=/tmp/angor-wallet-resume --timeout=180000
npm run build
npm test -- --watch=false --browsers=ChromeHeadless --include='**/indexer.service.spec.ts' --include='**/invest.component.spec.ts'
~/.local/share/angor-dotnet/dotnet build ../angor-blazor/src/webapp/Angor.Client/Angor.Client.csproj --no-restore
~/.local/share/angor-dotnet/dotnet test ../angor-blazor/src/shared/Angor.Shared.Tests/Angor.Shared.Tests.csproj --no-restore --filter 'FullyQualifiedName~WalletWordsAsyncTests|FullyQualifiedName~NetworkServiceTests|FullyQualifiedName~AddInputsFromAddressAndSignTransactionTests|FullyQualifiedName~PsbtOperationsTests'
```

Capture full test output to log files. A separate default .NET build can overwrite the live boot configuration and restore slow debugging; use the paired launcher for performance testing. The served `_framework/blazor.boot.json` should have `debugLevel: 0` and jiterpreter runtime options. Standalone `wallet-runtime.mjs` was an A/B diagnostic that overrides boot config; do not confuse its result with an unmodified live-server pass.

## Verification and remaining work

- Hub production build passed. **17 focused Hub tests passed** (`/tmp/angor-tests-testnet.log`).
- Blazor builds passed with existing warnings. **16 focused .NET tests passed**, including network races, wallet compatibility and signing/PSBT coverage (`/tmp/angor-final-dotnet-tests.log`).
- Full Hub tests had five failures also reproduced in a pristine baseline: three AppComponent tests, NostrAuth persistence, and NostrList auth effects (`/tmp/angor-tests-baseline.log`). Full lint had three existing errors and 79 warnings (`/tmp/angor-lint.log`); changed Angular files had no lint errors. Do not describe the full suite as green.
- Chromium 153 and Firefox 155 passed desktop/phone project handoff and loading checks. Funding pattern selection and schedule updates passed. Latest Firefox phone overflow check passed.
- WebKit 26.6 functional layout checks passed, including corrected phone overflow, but the runner remains red due to uncaught external indexer/CORS health-probe errors. See `/tmp/angor-pattern-final` and `/tmp/angor-pattern-phone-final`. Linux viewport emulation is not real-device Safari validation.
- The original fresh-wallet stall was about 24 seconds. After asynchronous derivation and optimized runtime, measurements were about 1.2–2.5 seconds. **Latest live run was ~2.45 seconds and failed the local 1.5-second responsiveness target**; its other checks passed (testnet invoice, tick/no toast, BIP-39 vector, preserving an existing wallet on a mismatched link). See `/tmp/angor-live-optimized`. Do not relax the timing assertion just to claim a pass; further cold-start work remains.
- No test funds were sent and no investment transactions were broadcast. Actual payment receipt, founder approval where required, publishing and completion/recovery still need end-to-end testnet validation. The Lightning button shares the tested copy component, but a live Lightning payment was not validated.

Next priorities: inspect remaining cold-start CPU work under the correct runtime configuration; finish the actual testnet payment/approval/completion flow; resolve or explicitly triage baseline tests/lint and WebKit network exceptions before declaring PR readiness. Coordinate the eventual PRs across both repositories.

Local `/tmp` logs can disappear. Some browser traces/flow logs contain disposable recovery phrases and browser state: **do not commit or upload raw wallet artifacts**. This note deliberately contains only public project identifiers and code/testing context.

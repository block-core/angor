# Local testnet stack for App.Test.Integration

A self-contained `docker compose` stack that reproduces the services the
Avalonia integration tests normally talk to on the public Angor signet, so
you can run the suite without depending on:

- `signet.angor.online` (indexer / mempool explorer)
- `faucettmp.angor.io`  (faucet API)
- `relay.angor.io` + `relay2.angor.io` (Nostr relays)

The compose brings up a fully isolated signet — own chain, own mining, no
outbound peers.

## What's inside

| Service            | Image                                  | Host URL                        |
|--------------------|----------------------------------------|---------------------------------|
| Bitcoin signet node| built from `block-core/bitcoin-custom-signet` | `http://localhost:48332` (RPC), `:48333` (P2P) |
| Fulcrum (Electrum) | `cculianu/fulcrum:latest`              | `tcp://localhost:48001`         |
| mempool backend    | `mempool/backend:latest`               | `http://localhost:48999`        |
| mempool frontend   | `mempool/frontend:latest`              | `http://localhost:48080`        |
| Nostr relay #1     | `dockurr/strfry:latest`                | `ws://localhost:47777`          |
| Nostr relay #2     | `dockurr/strfry:latest`                | `ws://localhost:47778`          |
| Faucet API         | built from `block-core/bitcoin-custom-signet/faucet-api` | `http://localhost:48500` |

The signet node has the miner enabled — it produces a block every ~30
seconds and pays the coinbase reward straight into the faucet wallet, so
the faucet is funded automatically as the chain grows. Expect a couple
of minutes before the faucet has enough confirmed coinbase coins to send.

## Prerequisites

- Docker Desktop (Windows/macOS) or Docker Engine + compose plugin (Linux).
- ~2 GB RAM and ~2 GB disk free for the containers and chain data.
- The two custom images (`bitcoin-signet`, `faucet-api`) are built from
  the GitHub repo on first `up`, which takes a few minutes. Subsequent
  starts reuse the cached images.

## Usage

```powershell
cd src/design/App.Test.Integration/docker

# optional: override ports or chain identity
Copy-Item .env.example .env

docker compose up -d
docker compose logs -f signet-node    # watch blocks being mined
```

Tear down (and wipe chain / relay data):

```powershell
docker compose down -v
```

## Pointing the Avalonia app at the local stack

The app honors three environment variables read at startup by
`CompositionRoot`. Setting them makes a fresh profile default to the
docker endpoints — no Settings-screen edits required.

```powershell
$env:ANGOR_INDEXER_URL     = "http://localhost:48080"
$env:ANGOR_RELAY_URLS      = "ws://localhost:47777,ws://localhost:47778"
$env:ANGOR_FAUCET_BASE_URL = "http://localhost:48500"
```

Unset them (or close the shell) to revert to the public Angor signet.

If you prefer the UI path, open the app, go to **Settings**, and replace
the indexer / relay / faucet entries by hand — same effect.

Then run the integration tests from `src/design/App.Test.Integration`.

## Smoke tests

```powershell
# 1. Node is up and mining
curl -u rpcuser:rpcpassword `
  --data-binary '{"jsonrpc":"1.0","id":"t","method":"getblockchaininfo","params":[]}' `
  -H 'content-type: text/plain;' `
  http://localhost:48332/

# 2. Mempool web exposes the chain tip
curl http://localhost:48080/api/v1/blocks/tip/height

# 3. Faucet is healthy
curl http://localhost:48500/api/faucet/network/status

# 4. Send coins to any signet (tb1...) address
curl "http://localhost:48500/api/faucet/send/<tb1...>/0.001"

# 5. Relays accept connections
#    (requires `websocat`; any Nostr client works too)
websocat ws://localhost:47777
> ["REQ","x",{}]
```

## Chain identity

The signet uses a **unique challenge** (derived from the secp256k1
generator point — well-known k=1 test key). This guarantees the local
chain can never merge with the public Angor signet even if a peer were
reachable. The challenge, privkey, and miner reward address are all
overridable via `.env`; regenerate with `gen-signet-keys.sh` from
[block-core/bitcoin-custom-signet](https://github.com/block-core/bitcoin-custom-signet)
if you want your own identity.

Ports use the 48xxx / 47xxx range specifically so this stack can run
alongside a public-Angor signet stack without clashing.

## Known follow-ups

- The two custom images build from git each time the cache is invalidated.
  Publishing `blockcore/bitcoin-signet` and `blockcore/faucet-api` to a
  registry would drop first-run time significantly.
- A CI job that spins up this stack and runs the integration suite
  headlessly is not yet wired up.

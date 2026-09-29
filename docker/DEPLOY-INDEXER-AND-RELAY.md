# Deploying a New Indexer and a New Nostr Relay

This guide walks through standing up your own **blockchain indexer** (a Mempool.space instance)
and your own **Nostr relay** (strfry) for use with Angor — either for mainnet, for Angor's custom
testnet ("Angornet"), or purely for local development/testing.

All Docker Compose files referenced here already exist in this repo under `docker/`, so in most
cases you're just running `docker compose up -d` with the right environment variables, not
writing new config from scratch.

## Prerequisites (all deployments)

- A Linux VPS or server with Docker + Docker Compose v2 installed (`docker compose version`).
- Outbound internet access (to pull images and, for the indexer, to sync the chain).
- A domain name if you want a public HTTPS endpoint (not required for local/dev use).

---

## Part 1 — Deploying a New Indexer (Mempool.space instance)

The indexer provides address lookups, transaction history, and fee estimates that the Angor app
depends on. It is **not** a custom Angor component — it's a Mempool.space (`mempool/backend` +
`mempool/frontend`) instance backed by an Electrum server (Fulcrum), pointed at either mainnet
Bitcoin or Angor's custom signet ("Angornet"). Decide which network you're indexing first, since
the two composes are quite different.

### Option A — Indexer for Angornet (custom signet testnet)

This is the easiest option to stand up because the compose file is **self-contained** — it
includes the signet node itself, so you don't need a separate Bitcoin Core install.

**Location:** `docker/explorers/angornet/docker-compose.yml`

1. **Build the custom signet node image locally** — it is *not* published to Docker Hub:
   ```bash
   git clone https://github.com/block-core/bitcoin-custom-signet.git
   cd bitcoin-custom-signet
   docker build -t blockcore/bitcoin-signet:latest .
   ```
   (See that repo's own README for exact build instructions/Dockerfile location if it has
   changed.)

2. Copy `docker/explorers/angornet/docker-compose.yml` to your server (or clone this repo there).

3. Review/override the environment variables at the top of the `node` service if needed:
   - `SIGNETCHALLENGE` — must match Angor's signet challenge (already set correctly in the file;
     don't change this unless you are deliberately running a different signet).
   - `ADDNODE` — the Angor signet seed peer (`207.180.254.78`); leave as-is so your node syncs
     from the real Angornet chain instead of starting an isolated one.
   - `RPCUSER` / `RPCPASSWORD` — change from the defaults for anything beyond local testing.

4. Start the stack:
   ```bash
   cd docker/explorers/angornet
   docker compose up -d
   ```

5. Wait for the signet node to sync. Check progress with:
   ```bash
   docker exec angornet-btc-node bitcoin-cli -signet getblockchaininfo
   ```
   Then confirm Fulcrum has caught up:
   ```bash
   docker logs -f angornet-fulcrum
   ```

6. Once Fulcrum reports it's caught up, the Mempool backend/frontend will start serving. Verify:
   ```bash
   curl http://localhost:8080/api/v1/fees/recommended
   ```

7. **Exposed ports** you'll need to forward/proxy externally:
   | Port | Purpose |
   |---|---|
   | 8080 | Mempool frontend + API (this is your public indexer URL) |
   | 8999 | Mempool backend API (internal only — proxied by the frontend) |
   | 38333 | Signet P2P (only needed if you want other nodes to peer with you) |
   | 38332 | Signet RPC (keep this firewalled off from the public internet) |

8. Put TLS + a public hostname in front of port 8080. Angor's own deployment uses
   [FRP](https://github.com/fatedier/frp) tunnels + [Caddy](https://caddyserver.com/) on a
   separate VPS (see the [deploy-proxy-frp](https://github.com/block-core/deploy-proxy-frp) repo
   for that exact setup), but any reverse proxy (nginx, Caddy, Traefik) terminating TLS and
   forwarding to `your-host:8080` works fine.

9. Point the Angor app at your new indexer. In the app: **Settings** → set the custom indexer URL
   to `https://your-domain/` (or, for local dev, set the `ANGOR_INDEXER_URL` environment variable
   before launching `App.Desktop` — see `src/design/App.Test.Integration/docker/README.md` for
   how the integration test stack does this).

### Option B — Indexer for Mainnet

Use this if you already run (or plan to run) your own **Bitcoin Core** node on the host machine.
Fulcrum (the Electrum server needed for address indexing) and the Mempool frontend/backend now
run in Docker as part of this compose file — you no longer need to install Fulcrum separately.

**Location:** `docker/explorers/mainnet/docker-compose.yml`

**Prerequisites (must exist on the host, not in this compose file):**
- Bitcoin Core, fully synced, with:
  - RPC enabled (default port 8332)
  - `txindex=1` (required by Fulcrum)
  - ZMQ block/tx notifications enabled in `bitcoin.conf`:
    ```
    zmqpubrawblock=tcp://0.0.0.0:28332
    zmqpubrawtx=tcp://0.0.0.0:28333
    ```

1. Copy `docker/explorers/mainnet/docker-compose.yml` to your server.
2. Set environment variables (via a `.env` file next to the compose file, or exported in your
   shell) so the Fulcrum container can reach your host's Bitcoin Core:
   ```bash
   CORE_RPC_HOST=172.17.0.1      # or host.docker.internal, or your host's LAN IP
   CORE_RPC_PORT=8332
   CORE_RPC_USERNAME=rpcuser
   CORE_RPC_PASSWORD=<your-real-rpc-password>
   ```
   `172.17.0.1` is the default Docker bridge gateway IP, which lets containers reach services
   bound on the host — confirm it matches your Docker network (`docker network inspect bridge`).
3. Start the stack:
   ```bash
   cd docker/explorers/mainnet
   docker compose up -d
   ```
4. Fulcrum needs to build its own address index against your Bitcoin Core node before the
   indexer is useful — this can take a while on first run. Follow progress with:
   ```bash
   docker logs -f mainnet-fulcrum
   ```
5. Once Fulcrum has caught up, verify the Mempool API:
   ```bash
   curl http://localhost:8189/api/v1/fees/recommended
   ```
6. **Exposed ports:**
   | Port | Purpose |
   |---|---|
   | 8189 | Mempool frontend + API (public indexer URL) |
   | 8999 | Mempool backend API (internal only) |
   | 50001 | Fulcrum electrum TCP (internal — only expose if other tools need direct Electrum access) |
7. Put TLS + a public hostname in front of port 8189, same as Option A step 8.
8. Point the Angor app at it via **Settings** (custom indexer URL) or the `ANGOR_INDEXER_URL` env
   var.

### Notes common to both options

- Both options now use the **standard, unmodified Mempool.space images** (`mempool/backend` /
  `mempool/frontend`) with **no custom fork and no `ANGOR_ENABLED` flag** — any stock Mempool.space
  instance works as-is. (An older Angor deployment used a custom `blockcore/mempool-*` fork with
  an `ANGOR_ENABLED` flag; this is no longer required.)
- Both options run their own **Fulcrum** container for address indexing — Angornet's Fulcrum
  points at the in-compose signet node; mainnet's Fulcrum points at your host's Bitcoin Core.
- Data persists in named Docker volumes (`*-mempool-cache`, `*-mempool-db`, `*-btc-data`,
  `*-fulcrum-data`). Back these up if you care about not re-syncing/re-indexing from scratch.
- Angor's own live instances for reference: mainnet `https://indexer.angor.io`, testnet
  `https://test.indexer.angor.io`.

---

## Part 2 — Deploying a New Nostr Relay (strfry)

The relay stores/serves the Nostr events that carry Angor project metadata (profiles, updates,
etc.). Angor uses [strfry](https://github.com/hoytech/strfry) via the community Docker image
`dockurr/strfry`. This is a good option for project founders who want to host a relay for their
own community.

**Location:** `docker/relays/docker-compose.yml` and `docker/relays/strfry.conf`

### Steps

1. Copy the `docker/relays/` directory to your server, preserving the layout — the compose file
   expects `./strfry/` (data dir) and `./strfry/strfry.conf` (config) relative to itself:
   ```bash
   mkdir -p docker/relays/strfry
   cp docker/relays/strfry.conf docker/relays/strfry/strfry.conf
   ```
   (Adjust: the compose file currently mounts `./strfry/strfry.conf` — make sure your config file
   ends up at exactly that path relative to the compose file.)

2. Edit `strfry.conf` before first start (some settings require a restart to change later, but
   are much easier to get right from the start):
   - `relay.info.name` — short relay name shown to clients (NIP-11), e.g. `"My Angor Relay"`.
   - `relay.info.description` — free-form description.
   - `relay.info.pubkey` / `relay.info.contact` — your admin nostr pubkey / contact email, so
     users know who runs it.
   - Leave `db`, `port` (7777), and the threading/negentropy settings at their defaults unless you
     have a specific reason to change them.

3. This compose file assumes an **external nginx-proxy + acme-companion (Let's Encrypt) stack**
   is already running on the host, on a Docker network named `proxy`. If you don't have one:
   ```bash
   docker network create proxy
   docker run -d --name nginx-proxy --network proxy -p 80:80 -p 443:443 \
     -v /var/run/docker.sock:/tmp/docker.sock:ro \
     -v certs:/etc/nginx/certs -v vhost:/etc/nginx/vhost.d -v html:/usr/share/nginx/html \
     nginxproxy/nginx-proxy
   docker run -d --name acme-companion --network proxy \
     --volumes-from nginx-proxy \
     -v /var/run/docker.sock:/var/run/docker.sock:ro \
     -v acme:/etc/acme.sh \
     nginxproxy/acme-companion
   ```
   (This is the standard `nginxproxy/nginx-proxy` + `nginxproxy/acme-companion` pairing that
   reads the `VIRTUAL_HOST`/`LETSENCRYPT_HOST` env vars automatically — you only need to set this
   up once per host, even if you later add more services behind it.)

4. Edit the environment variables in `docker-compose.yml` for your own domain:
   ```yaml
   environment:
     VIRTUAL_HOST: relay.yourdomain.com
     VIRTUAL_PORT: 7777
     VIRTUAL_PROTO: http
     VIRTUAL_NETWORK: proxy
     LETSENCRYPT_HOST: relay.yourdomain.com
     LETSENCRYPT_EMAIL: you@yourdomain.com
   ```
   Point your domain's DNS `A`/`AAAA` record at the server before starting, so Let's Encrypt can
   validate it.

5. Start the relay:
   ```bash
   cd docker/relays
   docker compose up -d
   ```

6. Verify:
   ```bash
   # Websocket handshake (should not error)
   curl -i -N -H "Connection: Upgrade" -H "Upgrade: websocket" \
     -H "Sec-WebSocket-Version: 13" -H "Sec-WebSocket-Key: test==" \
     http://localhost:7777

   # NIP-11 relay info document
   curl -H "Accept: application/nostr+json" http://localhost:7777
   ```
   Once TLS is up: `wss://relay.yourdomain.com` should be reachable from any Nostr client.

7. Optional web view: the `web` service (`getumbrel/umbrel-nostr-relay`) is exposed on port 3000
   for a simple browser view of relay activity — reachable at
   `http://relay.yourdomain.com:3000` (not proxied through nginx-proxy in the sample config).

8. Point the Angor app at your relay: **Settings** → add `wss://relay.yourdomain.com` to the list
   of relays. Angor's default relays (for reference) are `wss://relay.angor.io` and
   `wss://relay2.angor.io`; mainnet additionally includes public relays like
   `wss://relay.damus.io` and `wss://nos.lol`. You can run yours alongside or instead of these.

### Notes

- Data lives in `./strfry/` (LMDB database) on the host via a bind mount — back this directory up
  if you want to preserve relay history.
- `strfry.conf` has a `writePolicy.plugin` option (commented out) if you want to restrict who can
  publish to your relay (e.g. only allow specific pubkeys) — point it at an executable script; see
  [strfry's own docs](https://github.com/hoytech/strfry) for the plugin protocol.
- `rejectEventsOlderThanSeconds` / `ephemeralEventsLifetimeSeconds` control retention — defaults
  are generous (about 3 years for normal events); tune if disk space is a concern.

---

## Part 3 — Quick Local Dev Alternative (no public hosting needed)

If you just want an indexer + relay for local development/testing (not a public deployment),
there's already a fully self-contained stack used by the integration tests:

**Location:** `src/design/App.Test.Integration/docker/docker-compose.yml`
(see the accompanying `README.md` in that folder for exact usage)

This spins up, on `localhost` only:
- A private signet node + Fulcrum + Mempool indexer at `http://localhost:48080`
- Two strfry relays at `ws://localhost:47777` and `ws://localhost:47778`
- A faucet API at `http://localhost:48500` for funding test wallets

Start it with:
```bash
cd src/design/App.Test.Integration/docker
docker compose up -d
```

Then launch the app pointed at this stack via environment variables:
```bash
ANGOR_INDEXER_URL=http://localhost:48080 \
ANGOR_RELAY_URLS=ws://localhost:47777,ws://localhost:47778 \
ANGOR_FAUCET_BASE_URL=http://localhost:48500 \
dotnet run --project src/design/App.Desktop
```
This is the fastest way to get an isolated indexer + relay pair running for development without
touching DNS, TLS, or any of the reverse-proxy setup described above.

---

## Reference: Live Angor Endpoints

| Service | Network | URL |
|---|---|---|
| Indexer | Mainnet | `https://indexer.angor.io` |
| Indexer | Testnet (Angornet) | `https://test.indexer.angor.io` |
| Relay | Both | `wss://relay.angor.io`, `wss://relay2.angor.io` |
| Faucet | Testnet | `https://test.faucet.angor.io/api/faucet/send/{address}/{amount}` |
| Boltz (swaps) | Testnet | `https://test.boltz.angor.io` |

Use these as a sanity check — e.g. compare your new indexer's `/api/v1/fees/recommended` response
against `https://test.indexer.angor.io/api/v1/fees/recommended` to confirm it's returning sane
data.

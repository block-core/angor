# Deploying Angor Services with docker-compose

This guide will show you how to deploy Angor services, to allow users to self-host and gain more privacy.

## Table of Contents
- [Overview](#overview)
- [Testnet Deployments](#testnet-deployments)
  - [Custom Signet (Angornet)](#custom-signet-angornet)
  - [Indexer / Explorer](#indexer--explorer)
  - [Boltz Exchange](#boltz-exchange)
  - [Faucet](#faucet)
- [Deploy Explorers and Indexers](#deploy-explorers-and-indexers)
- [Deploy The Angor Application](#deploy-the-angor-application)
- [Deploy The Angor Hub](#deploy-the-angor-hub)
- [Deploy Nostr Relays](#deploy-nostr-relays)

## Overview

Angor services can be deployed using Docker Compose, enabling users to run the services on their own infrastructure for enhanced privacy and control.

### What Can You Self-Host?
- **The Angor App**: Build and run the Angor app locally or on a VPS instead of pulling the package from GitHub.
- **The Angor Hub**: Host the Angor Hub to manage and coordinate multiple Angor projects, allowing a user to filter what projects they think are a good investment.
- **Explorers and Blockchain Indexers**: Host specialized indexers required by Angor (future updates may remove this dependency for the web app).
- **Nostr Relays**: Ideal for project founders who want to host relays themselves to serve their community and investors.
- **Boltz Exchange**: Non-custodial swap service for Bitcoin, Lightning, and Liquid — used for onboarding and liquidity on the testnet.

## Testnet Deployments

Angor runs a custom signet network ("Angornet") for testing. The testnet infrastructure consists of three main components that work together:

### Custom Signet (Angornet)

Angor uses a custom Bitcoin signet network for development and testing. The signet node is the foundation that all other testnet services depend on.

- The custom signet node runs Bitcoin Core with a custom signetchallenge
- Peers connect to the Angor signet seed node to sync the chain
- The node exposes RPC (port 38332) and ZMQ interfaces for other services

For creating and running your own custom signet, refer to the [bitcoin-custom-signet](https://github.com/block-core/bitcoin-custom-signet) repository. That repo contains the Docker setup, signetchallenge configuration, and mining scripts needed to operate a custom signet.

### Indexer / Explorer

The blockchain indexer provides address lookups, transaction history, and fee estimates for the Angor app. It runs a [Mempool](https://github.com/mempool/mempool) instance backed by an Electrum server (Fulcrum) that indexes the custom signet chain.

- **Directory**: [`explorers/angornet/`](explorers/angornet/)
- **Live testnet instance**: [test.indexer.angor.io](https://test.indexer.angor.io)
- **Live mainnet instance**: [indexer.angor.io](https://indexer.angor.io) (config in [`explorers/mainnet/`](explorers/mainnet/))
- **Components**: Bitcoin signet node, Fulcrum (Electrum server), Mempool backend, Mempool frontend, MariaDB
- **Depends on**: A running custom signet node on the same Docker network

### Boltz Exchange

[Boltz](https://boltz.exchange) is a non-custodial swap service that enables trustless swaps between on-chain Bitcoin, Lightning, and Liquid. On the testnet, it provides a way to move funds between layers without requiring trust in a third party.

- **Directory**: [`boltz/`](boltz/)
- **Live testnet instance**: [test.boltz.angor.io](https://test.boltz.angor.io)
- **Components**:
  - **2 LND nodes** (lnd-1, lnd-2) and **2 CLN nodes** (cln-1, cln-2) — Lightning nodes for processing swaps
  - **Boltz backend** — swap engine that coordinates between chains and Lightning
  - **Elements node** — Liquid sidechain node for L-BTC swaps
  - **Electrs** — lightweight Electrum server indexes for both Bitcoin signet and Liquid
  - **PostgreSQL** — database for swap state
  - **Redis** — caching layer
  - **Nginx** — API reverse proxy
  - **Web app** — the Boltz swap UI
  - **ThunderHub** — web UI for managing the LND wallets ([thub1](https://test.thub1.angor.io), [thub2](https://test.thub2.angor.io))
  - **RTL (Ride The Lightning)** — web UI for managing the CLN wallets
- **Depends on**: A running custom signet node on the same Docker network (`angornet-network`)
- **Key config notes**:
  - `network = "signet"` must be the first line in `boltz.conf`
  - CLN nodes use `--force-feerates=1000/500/253/253` because custom signets cannot estimate fees
  - LND nodes use `--bitcoin.signet` without `--bitcoin.signetchallenge`
  - The cleanup container must stay disabled (custom signet, not regtest)

### Faucet

The faucet gives users testnet signet coins so they can try out Angor without needing real bitcoin. There are two faucet mechanisms:

**Web Faucet API** — a hosted HTTP endpoint that the Angor app calls directly when a user requests test coins:
- **Live endpoint**: `https://faucettmp.angor.io/api/faucet/send/{address}/{amount}`
- The Angor app calls this automatically from the wallet UI to fund new wallets
- Sends signet BTC from a pre-funded wallet to the requested address

**Miner Faucet** (programmatic) — a C# helper class used in integration tests to fund test addresses automatically from the signet miner wallet:
- **Location**: `src/Angor/Avalonia/Angor.Sdk.Tests/Funding/TestDoubles/AngornetMinerFaucet.cs`
- Uses the miner wallet (which always has funds from block rewards) to send coins to test addresses
- Handles UTXO selection, transaction signing, and confirmation waiting
- Eliminates manual funding steps in automated tests

### Network Architecture

All testnet services share a single Docker network (`angornet-network`) so containers can communicate by hostname. Services are exposed to the internet via [FRP](https://github.com/fatedier/frp) reverse proxy tunnels terminated with HTTPS by [Caddy](https://caddyserver.com/) on a VPS. See the [deploy-proxy-frp](https://github.com/block-core/deploy-proxy-frp) repo for the reverse proxy setup.

```
                                    angor-miner (192.168.1.109)
                                  ┌──────────────────────────────────┐
Internet ──► VPS (Caddy + FRP) ──►│  FRP client                     │
                                  │    ├─► :8080  Mempool (indexer)  │
                                  │    ├─► :8082  Boltz web app      │
                                  │    ├─► :8083  ThunderHub LND-1   │
                                  │    └─► :8084  ThunderHub LND-2   │
                                  │                                  │
                                  │  angornet-network (Docker)       │
                                  │    ├── signet node (:38332)      │
                                  │    ├── boltz stack (18 containers)│
                                  │    └── mempool stack             │
                                  └──────────────────────────────────┘
```

## Deploy Explorers and Indexers

Docker Compose files for running blockchain explorers and indexers are in the `explorers/` directory:

- **`explorers/mainnet/`** — Mainnet explorer stack (mempool backend + frontend + MariaDB). Connects to a Bitcoin Core node and Fulcrum running natively on the host.
- **`explorers/angornet/`** — Angor testnet (signet) explorer stack (custom signet node + Fulcrum + mempool backend + frontend + MariaDB). Self-contained, syncs from the Angor signet peer.

## Deploy The Angor Application

To deploy the Angor application, navigate to the `angor-app` directory and follow the instructions in the [Angor App README](/angor-app/readme.md).

## Deploy The Angor Hub

To deploy the Angor Hub, navigate to the `angor-hub` directory and follow the instructions in the [Angor Hub README](/angor-hub/readme.md).

## Deploy Nostr Relays

Nostr relay configuration is in the `relays/` directory. This runs a [strfry](https://github.com/hoytech/strfry) relay that project founders can host to serve their community and investors.

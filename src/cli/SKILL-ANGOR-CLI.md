# Angor CLI / MCP Server — AI Agent Skill

> **WARNING: EXPERIMENTAL** — This CLI and MCP server are experimental. Commands and tools may change, break, or produce unexpected results without notice. Do not rely on this for production workflows.

## What This Is

The Angor CLI is a headless .NET console app at `src/cli/Angor.Cli/` that exposes the full Angor SDK as both a command-line tool and an MCP (Model Context Protocol) server. Use it to manage wallets, browse projects, invest, recover funds, and more — all without a GUI.

## Building & Running

```bash
# Build
dotnet build src/cli/Angor.Cli/Angor.Cli.csproj

# Run a CLI command
dotnet run --project src/cli/Angor.Cli -- <command> [options]

# Start as MCP server (for AI agent use)
dotnet run --project src/cli/Angor.Cli -- --mcp
```

## Environment Variables

| Variable | Purpose | Required |
|----------|---------|----------|
| `ANGOR_WALLET_PASSWORD` | Wallet encryption password | Required in MCP mode, optional in CLI (prompts if missing) |
| `ANGOR_WALLET_PASSPHRASE` | BIP39 passphrase (empty string if none) | Optional |

## MCP Server Configuration

### OpenCode

Add to your MCP config:

```json
{
  "mcpServers": {
    "angor": {
      "command": "dotnet",
      "args": ["run", "--project", "src/cli/Angor.Cli", "--", "--mcp"],
      "env": { "ANGOR_WALLET_PASSWORD": "<password>" }
    }
  }
}
```

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "angor": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/src/cli/Angor.Cli", "--", "--mcp"],
      "env": { "ANGOR_WALLET_PASSWORD": "<password>" }
    }
  }
}
```

## Command Reference

All commands support `--json` for structured output where applicable.

### Wallet Commands

```bash
wallet list                                          # List all wallets
wallet create --name <n> [--network testnet|mainnet] [--seed "words..."]
wallet balance --wallet-id <id>                      # Balance in sats
wallet balance-detail --wallet-id <id>               # Detailed balance info
wallet receive --wallet-id <id>                      # Next receive address
wallet send --wallet-id <id> --address <addr> --amount <sats> --fee-rate <sat/vB>
wallet transactions --wallet-id <id>                 # List transactions
wallet fee-estimates                                 # Current fee estimates
wallet estimate-fee --wallet-id <id> --address <addr> --amount <sats> --fee-rate <sat/vB>
wallet test-coins --wallet-id <id>                   # Request testnet coins
wallet delete --wallet-id <id>                       # Delete a wallet
wallet generate-seed                                 # Generate BIP39 seed words
```

### Project Commands

```bash
project list                                         # Latest projects
project get --id <project-id>                        # Full project details
project try-get --id <project-id>                    # Get project (returns null if not found)
project stats --id <project-id>                      # Project statistics
project info-json --id <project-id>                  # Raw project info JSON
project profile --id <project-id>                    # Profile data (description, FAQ, etc.)
```

### Founder Commands

```bash
founder create-keys --wallet-id <id>                 # Generate project keys
founder my-projects --wallet-id <id>                 # List my projects
founder scan-projects --wallet-id <id>               # Scan network for my projects
founder investments --wallet-id <id> --project-id <pid>  # List investments
founder approve --wallet-id <id> --project-id <pid> --event-id <eid> --investor-pubkey <pk> --tx-hex <hex> --amount <sats>
founder claimable --wallet-id <id> --project-id <pid>    # Claimable transactions
founder releasable --wallet-id <id> --project-id <pid>   # Releasable transactions
founder release --wallet-id <id> --project-id <pid> --event-ids <id1> <id2>
founder spend-stage --wallet-id <id> --project-id <pid> --fee-rate <sat/vB> --stage-id <n> --address <addr>
founder submit-tx --tx-hex <hex> --tx-id <txid> --fee <sats>
founder moonshot --event-id <eid>                    # Moonshot project data
```

### Investor Commands

```bash
investor build-draft --wallet-id <id> --project-id <pid> --amount <sats> --fee-rate <sat/vB>
investor my-investments --wallet-id <id>             # List investments
investor total-invested --wallet-id <id>             # Total invested across all projects
investor penalties --wallet-id <id>                  # List penalties
investor penalty-check --project-id <pid> --amount <sats>  # Check penalty threshold
investor recovery-status --wallet-id <id> --project-id <pid>
investor build-recovery --wallet-id <id> --project-id <pid> --fee-rate <sat/vB>
investor build-release --wallet-id <id> --project-id <pid> --fee-rate <sat/vB>
investor build-penalty-release --wallet-id <id> --project-id <pid> --fee-rate <sat/vB>
investor build-eop-claim --wallet-id <id> --project-id <pid> --fee-rate <sat/vB>
investor check-signatures --wallet-id <id> --project-id <pid>
investor submit-tx --tx-hex <hex> --tx-id <txid> --fee <sats> [--wallet-id <id>] [--project-id <pid>]
investor get-nsec --wallet-id <id> --founder-key <key>
```

### Lightning Commands

```bash
lightning create-swap --wallet-id <id> --claim-pubkey <pk> --amount <sats> --receiving-address <addr> --stage-count <n> [--fee-rate 2]
lightning monitor-swap --wallet-id <id> --swap-id <sid> [--timeout <seconds>]
```

### Config Commands

```bash
config show                                          # Show current config
config get-network                                   # Show current network
config set-network <testnet|mainnet>                 # Switch network (restart required)
```

## Common Workflows

### Create a testnet wallet and fund it

```bash
dotnet run --project src/cli/Angor.Cli -- wallet create --name "TestWallet" --network testnet
# Note the wallet ID from output
dotnet run --project src/cli/Angor.Cli -- wallet test-coins --wallet-id <id>
dotnet run --project src/cli/Angor.Cli -- wallet balance --wallet-id <id>
```

### Browse projects

```bash
dotnet run --project src/cli/Angor.Cli -- project list --json
dotnet run --project src/cli/Angor.Cli -- project get --id <project-id> --json
dotnet run --project src/cli/Angor.Cli -- project stats --id <project-id>
```

### Founder: create and manage a project

```bash
# 1. Generate project keys
dotnet run --project src/cli/Angor.Cli -- founder create-keys --wallet-id <id>

# 2. List your projects
dotnet run --project src/cli/Angor.Cli -- founder my-projects --wallet-id <id>

# 3. View investments
dotnet run --project src/cli/Angor.Cli -- founder investments --wallet-id <id> --project-id <pid>

# 4. Approve an investment
dotnet run --project src/cli/Angor.Cli -- founder approve --wallet-id <id> --project-id <pid> --event-id <eid> --investor-pubkey <pk> --tx-hex <hex> --amount <sats>
```

### Investor: invest and recover

```bash
# 1. Build investment draft
dotnet run --project src/cli/Angor.Cli -- investor build-draft --wallet-id <id> --project-id <pid> --amount 100000 --fee-rate 2

# 2. Check recovery status
dotnet run --project src/cli/Angor.Cli -- investor recovery-status --wallet-id <id> --project-id <pid>

# 3. Build recovery transaction if needed
dotnet run --project src/cli/Angor.Cli -- investor build-recovery --wallet-id <id> --project-id <pid> --fee-rate 2
```

## Running Tests

```bash
dotnet test src/cli/Angor.Cli.Tests/Angor.Cli.Tests.csproj
```

## 48 MCP Tools

When running as an MCP server (`--mcp`), 48 tools are exposed across 6 groups:

| Group | Tools | Count |
|-------|-------|-------|
| Wallet | WalletList, WalletCreate, WalletBalance, WalletBalanceDetail, WalletReceive, WalletSend, WalletTransactions, WalletFeeEstimates, WalletEstimateFee, WalletTestCoins, WalletDelete, WalletGenerateSeed | 12 |
| Project | ProjectList, ProjectGet, ProjectTryGet, ProjectStats, ProjectInfoJson, ProjectProfile, ProjectRelays | 7 |
| Founder | FounderCreateKeys, FounderMyProjects, FounderScanProjects, FounderInvestments, FounderApprove, FounderClaimable, FounderReleasable, FounderRelease, FounderSpendStage, FounderSubmitTx, FounderMoonshot | 11 |
| Investor | InvestorBuildDraft, InvestorMyInvestments, InvestorTotalInvested, InvestorPenalties, InvestorPenaltyCheck, InvestorRecoveryStatus, InvestorBuildRecovery, InvestorBuildRelease, InvestorBuildPenaltyRelease, InvestorBuildEopClaim, InvestorCheckSignatures, InvestorSubmitTx, InvestorGetNsec | 13 |
| Lightning | LightningCreateSwap, LightningMonitorSwap | 2 |
| Config | ConfigShow, ConfigGetNetwork, ConfigSetNetwork | 3 |

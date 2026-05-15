# Angor CLI / MCP Server - Implementation Plan

## Overview

A .NET 10 console application at `src/cli/Angor.Cli/` that exposes the Angor SDK as both a command-line tool and an MCP (Model Context Protocol) server. An AI agent can call any Angor operation — browse projects, manage wallets, invest, recover funds — through structured tool calls.

**Entry modes:**

```
angor-cli [command] [options]     # CLI mode (human / script use)
angor-cli --mcp                   # MCP server mode (JSON-RPC over stdio, AI agent use)
```

---

## Implementation Progress

| Phase | Status | Notes |
|-------|--------|-------|
| 1 — Scaffolding + headless DI | **DONE** | `Angor.Cli.csproj`, `CompositionRoot`, headless providers |
| 2 — Wallet commands | **DONE** | 12 CLI commands + 12 MCP tools |
| 3 — Project commands (read-only) | **DONE** | 6 CLI commands + 7 MCP tools |
| 6 — MCP protocol layer | **DONE** | `--mcp` flag, `ModelContextProtocol` 1.3.0, stdio transport |
| 4 — Founder commands | Pending | |
| 5 — Investor commands | Pending | |
| 7 — Lightning + config | Pending | |
| 8 — Testing + polish | Pending | |

### What was built (MVP)

- **Project:** `src/cli/Angor.Cli/Angor.Cli.csproj` targeting `net10.0`
- **DI:** Headless `CompositionRoot` mirroring `src/design/App/Composition/CompositionRoot.cs` — registers SDK wallet + funding services, LiteDB, Serilog file logging, platform-specific `ISecureKeyProvider` (DPAPI on Windows, Linux fallback on Linux/macOS). No UI dependencies.
- **Security:** `HeadlessPasswordProvider` (env var `ANGOR_WALLET_PASSWORD` or masked console prompt in CLI mode), `HeadlessPassphraseProvider` (env var `ANGOR_WALLET_PASSPHRASE`), `HeadlessSeedwordsProvider` (delegates to SDK's built-in `SeedwordsProvider`).
- **CLI commands:** `wallet` (12 subcommands) and `project` (6 subcommands) via `System.CommandLine`. All commands support `--json` for structured output.
- **MCP tools:** `WalletTools` (12 tools) and `ProjectTools` (7 tools) using `[McpServerToolType]` / `[McpServerTool]` attributes. Tools are auto-discovered via `WithToolsFromAssembly()`.
- **Packages added to `Directory.Packages.props`:** `System.CommandLine` 2.0.0-beta4, `ModelContextProtocol` 1.3.0, `Microsoft.Extensions.Hosting` 9.0.10.

### What was NOT needed

- **No business logic refactoring from UI to SDK.** All wallet and project operations were already fully implemented behind `IWalletAppService` and `IProjectAppService` in the SDK. The CLI simply wires up the same DI container and calls the same app services.
- **No `ConsoleSecurityContext`.** The SDK's `WalletSecurityContext` works as-is with the headless providers.
- **No separate `Cli.sln`.** The project builds standalone via `dotnet build src/cli/Angor.Cli/Angor.Cli.csproj`. Can be added to a solution later.

### Implementation decisions

- Targeted `net10.0` (matching current SDK/design TFM) instead of `net9.0` from the original plan.
- `LatestProjectsRequest` takes no constructor arguments (no offset/limit) — the SDK doesn't support pagination on that endpoint yet.
- `Balance` is a simple `record Balance(long Sats)` — no confirmed/unconfirmed split at the SDK level.
- MCP server builds a separate `IHost` and re-registers resolved SDK services from the headless `CompositionRoot` into the host's DI container.

---

## Critical Architectural Rule

> **If implementing a CLI command requires copying business logic from `src/avalonia/` or `src/design/`, that logic must be moved into the SDK first.**

Create a new MediatR operation or app service method in `Angor.Sdk`, then have both the UI app and the CLI consume it from there.

This means the CLI project doubles as a forcing function for architectural hygiene — any business logic that leaked into ViewModels gets caught and refactored back into the SDK where it belongs. Never duplicate business logic; always push it down.

---

## Project Structure

```
src/
  cli/
    PLAN.md                        This file
    Angor.Cli/                     net10.0 console app
      Angor.Cli.csproj
      Program.cs                   Entry point: CLI vs MCP mode
      Composition/
        CompositionRoot.cs         Headless DI (SDK + wallet, no UI)
        CliApplicationStorage.cs   IApplicationStorage for CLI
        HeadlessPasswordProvider.cs
        HeadlessPassphraseProvider.cs
        HeadlessSeedwordsProvider.cs
      Commands/                    CLI commands (System.CommandLine)
        Wallet/
          WalletCommands.cs        ✅ 12 commands implemented
        Projects/
          ProjectCommands.cs       ✅ 6 commands implemented
        Founder/                   ⬜ Phase 4
        Investor/                  ⬜ Phase 5
        Lightning/                 ⬜ Phase 7
        Config/                    ⬜ Phase 7
      McpTools/                    MCP tool wrappers
        WalletTools.cs             ✅ 12 tools implemented
        ProjectTools.cs            ✅ 7 tools implemented
        FounderTools.cs            ⬜ Phase 4
        InvestorTools.cs           ⬜ Phase 5
        LightningTools.cs          ⬜ Phase 7
        ConfigTools.cs             ⬜ Phase 7
    Angor.Cli.Tests/               ⬜ Phase 8
      Angor.Cli.Tests.csproj
```

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `System.CommandLine` | 2.0.0-beta4.22272.1 | CLI command parsing |
| `ModelContextProtocol` | 1.3.0 | Official C# MCP SDK |
| `Microsoft.Extensions.DependencyInjection` | 9.0.10 (CPM) | DI container |
| `Microsoft.Extensions.Hosting` | 9.0.10 | MCP server hosting |
| `Serilog.Sinks.File` | 7.0.0 (CPM) | Log to file (console is reserved for MCP stdio) |

---

## Phase 1: Project Scaffolding + Headless DI — ✅ DONE

### 1.1 Create project files

- `Angor.Cli.csproj` targeting `net9.0`
- Project references: `Angor.Sdk`, `Angor.Data.Documents.LiteDb`
- Add new packages to `Directory.Packages.props`
- Add project to a solution (new `Cli.sln` or existing `App.sln`)

### 1.2 CompositionRoot (headless)

Mirror the Avalonia `CompositionRoot` but strip all UI:

**Keep from Avalonia:**
- LiteDB document storage (`AddLiteDbDocumentStorage`)
- `IStore` (file-backed)
- Network configuration (`INetworkConfiguration`, `INetworkService`)
- `WalletContextServices.Register()`
- `FundingContextServices.Register()`
- MediatR registration
- `IHttpClientFactory`
- Serilog logging (to file, not console)

**Replace with headless implementations:**
- `IPasswordProvider` -> `HeadlessPasswordProvider` (env var `ANGOR_WALLET_PASSWORD` or stdin)
- `IPassphraseProvider` -> `HeadlessPassphraseProvider` (env var `ANGOR_WALLET_PASSPHRASE` or stdin)
- `ISeedwordsProvider` -> `HeadlessSeedwordsProvider` (stdout in CLI, tool response in MCP)
- `IDialog` -> not needed (operations return `Result<T>`, CLI prints errors)
- All ViewModels -> not needed
- All UI services -> not needed

**Add:**
- `ConsoleSecurityContext`: wraps `IWalletSecurityContext` with console-friendly prompts

### 1.3 Security in MCP mode

In MCP mode, stdin/stdout are reserved for JSON-RPC. Wallet password **must** be provided via environment variable — no interactive prompts. The `HeadlessPasswordProvider` detects MCP mode and fails with a clear error if no env var is set.

### 1.4 Program.cs entry point

```csharp
if (args.Contains("--mcp"))
    // Start MCP server with stdio transport
else
    // Parse CLI commands via System.CommandLine
```

### 1.5 Acceptance criteria

- `dotnet build` succeeds
- DI container resolves all app services without error
- `angor-cli --version` prints version
- `angor-cli --mcp` starts and responds to MCP `initialize` handshake

---

## Phase 2: Wallet Commands — ✅ DONE

### 2.1 CLI commands

| Command | Maps to | Args |
|---------|---------|------|
| `wallet create` | `IWalletAppService.CreateWallet` | `--name`, `--network`, `--seed` (optional) |
| `wallet create-nopass` | `CreateWalletWithoutPassword` | `--network` |
| `wallet list` | `GetMetadatas` | -- |
| `wallet balance` | `GetBalance` | `--wallet-id` |
| `wallet balance-detail` | `GetAccountBalanceInfo` | `--wallet-id` |
| `wallet send` | `SendAmount` | `--wallet-id`, `--address`, `--amount`, `--fee-rate` |
| `wallet receive` | `GetNextReceiveAddress` | `--wallet-id` |
| `wallet fee-estimates` | `GetFeeEstimates` | -- |
| `wallet estimate-fee` | `EstimateFeeAndSize` | `--wallet-id`, `--address`, `--amount`, `--fee-rate` |
| `wallet transactions` | `GetTransactions` | `--wallet-id` |
| `wallet test-coins` | `GetTestCoins` | `--wallet-id` |
| `wallet delete` | `DeleteWallet` | `--wallet-id` |
| `wallet generate-seed` | `GenerateRandomSeedwords` | -- |

### 2.2 MCP tools

Each command becomes an MCP tool. Tool names use snake_case: `wallet_create`, `wallet_balance`, etc.

### 2.3 Output format

- **CLI mode:** Human-readable table/text. `--json` flag for structured output.
- **MCP mode:** Always JSON in tool response content.

### 2.4 Acceptance criteria

- Create a testnet wallet, check balance, get receive address, send funds — all from CLI
- Same operations work via MCP tool calls
- Error cases (wrong password, insufficient funds) return clear messages, not stack traces

---

## Phase 3: Read-Only Project Commands — ✅ DONE

### 3.1 CLI commands

| Command | Maps to |
|---------|---------|
| `project list` | `IProjectAppService.Latest` |
| `project get` | `Get` |
| `project try-get` | `TryGet` |
| `project stats` | `GetProjectStatistics` |
| `project info-json` | `GetProjectInfoJson` |
| `project profile` | `FetchProjectProfileData` |
| `project relays` | `GetRelaysForNpubAsync` |

### 3.2 Acceptance criteria

- `angor-cli project list` returns projects from the indexer
- `angor-cli project get --id <id>` returns full project details
- AI agent can browse and inspect any project via MCP

---

## Phase 4: Founder Commands (3-4 hours)

### 4.1 CLI commands

| Command | Maps to |
|---------|---------|
| `founder create-keys` | `IFounderAppService.CreateProjectKeys` |
| `founder create-profile` | `IProjectAppService.CreateProjectProfile` |
| `founder create-info` | `IProjectAppService.CreateProjectInfo` |
| `founder create-project` | `IProjectAppService.CreateProject` |
| `founder update-profile` | `IProjectAppService.UpdateProjectProfile` |
| `founder my-projects` | `IProjectAppService.GetFounderProjects` |
| `founder scan-projects` | `IProjectAppService.ScanFounderProjects` |
| `founder investments` | `IFounderAppService.GetProjectInvestments` |
| `founder approve` | `IFounderAppService.ApproveInvestment` |
| `founder claimable` | `IFounderAppService.GetClaimableTransactions` |
| `founder releasable` | `IFounderAppService.GetReleasableTransactions` |
| `founder release` | `IFounderAppService.ReleaseFunds` |
| `founder spend-stage` | `IFounderAppService.SpendStageFunds` |
| `founder submit-tx` | `IFounderAppService.SubmitTransactionFromDraft` |
| `founder moonshot` | `IFounderAppService.GetMoonshotProject` |

### 4.2 Complex input handling

Commands like `create-project` and `update-profile` require structured input (stages, metadata, FAQ items). In CLI mode, accept `--input-file <path>` pointing to a JSON file. In MCP mode, accept JSON directly as tool arguments.

### 4.3 SDK refactoring checkpoint

Review all founder flows in `src/avalonia/` and `src/design/` ViewModels. Any business logic that lives in the ViewModel (validation, multi-step orchestration, data transformation) must be extracted into the SDK as a new MediatR operation before the CLI can use it.

### 4.4 Acceptance criteria

- Full project creation flow works end-to-end from CLI
- Founder can approve investments and release stage funds

---

## Phase 5: Investor Commands (3-4 hours)

### 5.1 CLI commands

| Command | Maps to |
|---------|---------|
| `investor build-draft` | `IInvestmentAppService.BuildInvestmentDraft` |
| `investor submit` | `SubmitInvestment` |
| `investor confirm` | `ConfirmInvestment` |
| `investor cancel` | `CancelInvestmentRequest` |
| `investor my-investments` | `GetInvestments` |
| `investor total-invested` | `GetTotalInvested` |
| `investor penalties` | `GetPenalties` |
| `investor penalty-check` | `IsInvestmentAbovePenaltyThreshold` |
| `investor recovery-status` | `GetRecoveryStatus` |
| `investor build-recovery` | `BuildRecoveryTransaction` |
| `investor build-release` | `BuildUnfundedReleaseTransaction` |
| `investor build-penalty-release` | `BuildPenaltyReleaseTransaction` |
| `investor build-eop-claim` | `BuildEndOfProjectClaim` |
| `investor check-signatures` | `CheckForReleaseSignatures` |
| `investor submit-tx` | `SubmitTransactionFromDraft` |
| `investor get-nsec` | `GetInvestorNsec` |

### 5.2 SDK refactoring checkpoint

Same as Phase 4 — review investor ViewModels for leaked business logic and push it into the SDK.

### 5.3 Acceptance criteria

- Full investment flow: build draft -> submit -> confirm -> monitor
- Recovery flow works end-to-end

---

## Phase 6: MCP Protocol Layer — ✅ DONE

### 6.1 MCP server setup

Using the `ModelContextProtocol` C# SDK:

```csharp
var builder = Host.CreateApplicationBuilder();
builder.Services
    .AddMcpServer()
    .WithStdioTransport()
    .WithTools<WalletTools>()
    .WithTools<ProjectTools>()
    .WithTools<FounderTools>()
    .WithTools<InvestorTools>()
    .WithTools<LightningTools>()
    .WithTools<ConfigTools>();
// + register all SDK services via CompositionRoot
await builder.Build().RunAsync();
```

### 6.2 Tool definitions

Each MCP tool class wraps the corresponding app service:

```csharp
[McpServerToolType]
public class WalletTools(IWalletAppService walletService)
{
    [McpServerTool, Description("Get wallet balance in satoshis")]
    public async Task<string> WalletBalance(string walletId)
    {
        var result = await walletService.GetBalance(new WalletId(walletId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value)
            : $"Error: {result.Error}";
    }
}
```

### 6.3 MCP resources (optional, nice-to-have)

Expose read-only data as MCP resources:
- `angor://projects/latest` — latest projects list
- `angor://wallet/{id}/balance` — wallet balance

### 6.4 Testing with AI agents

- Test with Claude Desktop (`claude_desktop_config.json`)
- Test with OpenCode (MCP server config)
- Verify all tools are discoverable and callable

### 6.5 Acceptance criteria

- `angor-cli --mcp` starts clean MCP server
- All ~45 tools appear in tool listing
- Full wallet + project browsing flow works from an AI agent

---

## Phase 7: Lightning + Config Commands (1-2 hours)

### 7.1 CLI commands

| Command | Maps to |
|---------|---------|
| `lightning create-swap` | `IInvestmentAppService.CreateLightningSwap` |
| `lightning monitor-swap` | `IInvestmentAppService.MonitorLightningSwap` |
| `config set-network` | Switch mainnet/testnet |
| `config get-network` | Show current network |
| `config show` | Show all settings (indexer URLs, relay URLs) |

### 7.2 Long-running operations

`lightning monitor-swap` and address monitoring are long-running:
- **CLI:** Poll with progress indicator, configurable `--timeout`
- **MCP:** Return current status immediately; AI agent polls by calling again

### 7.3 Acceptance criteria

- Network switching works and persists across sessions
- Lightning swap flow works end-to-end

---

## Phase 8: Testing + Polish (2-3 hours)

### 8.1 Integration tests (`Angor.Cli.Tests`)

- DI composition test: verify all services resolve
- Command parsing tests: verify all commands parse correctly
- MCP tool discovery test: verify all tools register
- End-to-end wallet test on testnet (if CI has network access)

### 8.2 Error handling

- All commands catch `Result.Failure` and print clean error messages
- Network errors: "Cannot reach indexer at X, check network config"
- Invalid arguments: show usage help

### 8.3 Documentation

- `README.md` in `src/cli/` with setup + usage examples
- MCP configuration snippets for Claude Desktop and OpenCode
- Example AI agent prompts

### 8.4 CI integration

- Add `Angor.Cli.csproj` build + test to `ci.yml`

---

## Timeline

| Phase | Scope | Estimate | Status |
|-------|-------|----------|--------|
| 1 | Scaffolding + headless DI | 2-3 hours | ✅ Done |
| 2 | Wallet commands | 2-3 hours | ✅ Done |
| 3 | Project commands (read-only) | 1-2 hours | ✅ Done |
| 4 | Founder commands | 3-4 hours | ⬜ Next |
| 5 | Investor commands | 3-4 hours | ⬜ |
| 6 | MCP protocol layer | 2-3 hours | ✅ Done |
| 7 | Lightning + config | 1-2 hours | ⬜ |
| 8 | Testing + polish | 2-3 hours | ⬜ |
| 9 | AI coding skill | 1-2 hours | ⬜ |
| **Total** | | **~18-27 hours (~2-3 days)** | **MVP done** |

**MVP (Phases 1-3 + 6): COMPLETE.** Wallet management + project browsing via CLI and MCP. An AI agent can explore projects, manage wallets, and check balances.

**Next up: Phase 4 (Founder commands)** — this is where SDK refactoring may be needed if founder flows have business logic in ViewModels.

---

## Tool Surface Summary

~45 operations total:

| Group | Count |
|-------|-------|
| Wallet | ~13 |
| Projects (read-only) | ~7 |
| Founder | ~15 |
| Investor | ~16 |
| Lightning | ~2 |
| Config | ~3 |

---

## Phase 9: AI Coding Skill (1-2 hours)

Create a skill file so that AI coding agents (OpenCode, Cursor, Claude Code, etc.) can use the Angor CLI as a tool when working on or with the Angor codebase.

### 9.1 Skill definition file

Create a skill markdown file (e.g. `src/cli/SKILL-ANGOR-CLI.md` or in the repo's `.opencode/skills/` directory) that an AI agent can load to learn how to use the CLI. The skill should contain:

**Context:**
- What the Angor CLI is and when to use it
- How to build and run it: `dotnet run --project src/cli/Angor.Cli -- <command>`
- Available command groups and their purpose

**Command reference:**
- Full list of all implemented CLI commands with arguments and example usage
- Common workflows (create wallet, check balance, browse projects, invest, etc.)
- Environment variables (`ANGOR_WALLET_PASSWORD`, `ANGOR_WALLET_PASSPHRASE`) and when to set them

**MCP server mode:**
- How to configure the CLI as an MCP server in `claude_desktop_config.json` or OpenCode MCP config
- Example MCP config snippet:
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

**Workflow examples for AI agents:**
- "Create a testnet wallet and fund it" — step-by-step commands
- "Browse projects and show details" — which commands to chain
- "Invest in a project" — full founder/investor flow (once Phases 4-5 are done)

### 9.2 Integration with AGENTS.md

Add a section to the repo's `AGENTS.md` pointing AI agents at the CLI skill and explaining when to use the CLI vs the SDK directly (e.g. use CLI for testing/validation, use SDK for code changes).

### 9.3 Acceptance criteria

- An AI agent loading the skill can immediately use the CLI without needing to explore the codebase
- MCP server config works out of the box with Claude Desktop and OpenCode
- Skill covers all implemented commands at time of writing

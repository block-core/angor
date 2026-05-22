# CreateProjectTest

## Purpose

Per-process UAT test for the full project creation lifecycle with extended verification steps. Exercises the complete founder flow: wallet creation, project deployment, Blossom image upload, project profile editing via Nostr, and verification that all changes persisted correctly.

## Architecture

- **Per-process isolation**: Founder runs in a separate App.Desktop process with a real Avalonia window
- **HTTP orchestration**: `TestAutomationClient` → `AutomationServer` (localhost HTTP)
- **Env vars**: `ANGOR_TEST_API=1` + `ANGOR_TEST_API_PORT=<port>`

## Profile

- Founder: `CreateProject-Founder`

## Scenario

1. **Create wallet and fund** — Generate wallet, fund via testnet faucet, wait for non-zero balance.
2. **Deploy investment project** — Walk through the 6-step wizard (type → profile → images → funding → stages → review), deploy via wallet payment.
3. **Upload images to Blossom** — Download the original picsum.photos images and re-upload them to `blossom.angor.io` via the BUD-02 PUT protocol with ephemeral Nostr key auth.
4. **Edit project profile** — Open the Edit Profile panel, update name, displayName, about, picture (blossom URL), banner (blossom URL), website, and project content. Save to Nostr relays.
5. **Verify changes** — Fetch the project profile from Nostr relays and assert all fields match the edited values.

## What This Validates

- Full create-project wizard pipeline (SDK: CreateProjectKeys, CreateProjectProfile, CreateProjectInfo, CreateProject, SubmitTransactionFromDraft)
- Blossom upload service (BUD-02 protocol with Nostr auth headers)
- Edit profile round-trip: load existing data from Nostr → modify → save → re-fetch → assert
- Nostr relay propagation (profile metadata kind 0 + project content events)

## Run

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~CreateProjectTest"
```

Verbose:

```bash
dotnet test "src/design/App.Test.Uat/App.Test.Uat.csproj" --filter "FullyQualifiedName~CreateProjectTest" --logger "console;verbosity=detailed"
```

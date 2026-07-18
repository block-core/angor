# Mobile (Android) UAT Test Enablement — Plan

Goal: run the existing `App.Test.Uat` end-to-end tests against a USB-connected Android
device (package `io.angor.app`), driven over adb, alongside the current desktop hosts.

Context branch: work follows PR #933 (`fix/mobile-stale-projects-after-network-switch`),
which fixed stale network-switch state on mobile. This plan was researched in that session.

## How UAT works today (desktop)

- Each test launches one `App.Desktop` process per user profile with env vars
  `ANGOR_TEST_API=1`, `ANGOR_TEST_API_PORT=<free port>`, `ANGOR_NETWORK`,
  `ANGOR_INDEXER_URL`, `ANGOR_RELAY_URLS`, `ANGOR_FAUCET_BASE_URL` and a `--profile` arg.
- `App/Automation/AutomationServer.cs` (compiled `#if DEBUG` only, started from
  `App.axaml.cs:66-69`) is a hand-rolled HTTP server over `TcpListener` — no HttpListener,
  works on Android.
- Tests poll `http://127.0.0.1:{port}/health` then drive the UI via
  `App.Test.Uat/Helpers/TestAutomationClient.cs` (click, type, set controls, composite
  flows in `App/Automation/AutomationFlows.cs`).
- `App.Test.Uat/Helpers/TestProcessHost.cs` launches/kills the processes, pipes
  stdout/stderr to log files, picks free ports (`GetFreePort`, lines ~166-173), finds the
  exe (`FindDesktopExe`, ~175-222).

## What already exists for Android

- `AutomationServer.cs:48-72`: on Android the server **always starts in Debug builds**
  (no env-var gate), on **fixed port 18721**, bound to `IPAddress.Any` — explicitly
  designed for `adb forward`.
- `App.Android/MainActivity.cs:190-205`: `PerfTabReceiver`, an exported BroadcastReceiver
  (`adb shell am broadcast -a io.angor.app.PERF_TAB --es tab "..."`) → precedent for
  adb-driven control and intent-extra reading.
- `App.Android/scripts/android-perf.sh`: existing adb harness (logcat tags `DOTNET`,
  `Prewarm`, `ShellPerf`, `ProjectsLoad`).

Manual smoke check (requires the setprop gate — see item 5):

```
adb shell setprop debug.angor.test_api 1
adb shell am force-stop io.angor.app
adb shell monkey -p io.angor.app 1
adb forward tcp:18721 tcp:18721
curl http://127.0.0.1:18721/health
```

## Work items (in order)

Status: items 1, 2, 3 and 5 implemented (uncommitted); item 4 pending. Manual smoke check
now requires `adb shell setprop debug.angor.test_api 1` before launch (see item 5).

### 1. Lifetime-agnostic window resolution (biggest blocker) — DONE

Implemented:
- `App/Automation/AutomationRoot.cs` (new): lifetime-agnostic `Resolve()` — desktop
  `MainWindow`, Android via `TopLevel.GetTopLevel(App.MainView)`, single-view fallback.
- `App.axaml.cs`: static `App.MainView` captured from the `IActivityApplicationLifetime`
  factory and the `ISingleViewApplicationLifetime` branch.
- `AutomationServer.GetMainWindow` and `AutomationFlows.RequireWindowAsync`/`GetMainWindow`
  now return `TopLevel`; all flow helper signatures switched `Window` → `TopLevel`
  (only `GetVisualDescendants` was used — mechanical).
- `AutomationFlows.NavigateToAsync`: detects a missing `DesktopSidebar` and falls back to
  driving `ShellViewModel.SelectedNavItem` / `NavigateToSettings` directly (mobile path).

Verified: CreateProjectTest passes on desktop after the refactor.

### 2. AndroidTestHost (parallel to TestProcessHost) — DONE

Implemented as `App.Test.Uat/Helpers/AndroidTestHost.cs`:
- Launch: `am force-stop` → optional `pm clear` (wipeData flag) → setprops →
  `adb forward tcp:<free local> tcp:18721` → `monkey -p io.angor.app 1` → poll `/health`
  via `TestAutomationClient` (same 60s pattern as desktop).
- Logs: full `adb logcat` captured to `Temp/opencode/test-android-<profile>-logcat.log`.
- Dispose: `am force-stop`, `forward --remove`, clears `debug.angor.test_api`, kills logcat.
- Overrides: `ANGOR_ADB` (adb path), `ANGOR_ANDROID_SERIAL` (device selection).
- One device = one app instance: multi-profile tests (MultiFund/MultiInvest) should mix
  desktop peers + the Android device as one actor.
- Not yet exercised against a real device (needs a Debug build installed).

### 3. Config injection on Android — DONE

Implemented via system properties (not intent extras — Avalonia 12 builds the framework at
Application level, before `MainActivity.OnCreate`, so extras arrive too late):
- `App.Android/DebugTestConfig.cs` (new, `#if DEBUG`): reads `debug.angor.*` properties via
  `getprop` and maps them onto the ANGOR_* env vars in-process. Called from
  `MainApplication.CustomizeAppBuilder` before the DI container is built, so
  `CompositionRoot`, `EnvOverrideNetworkStorage` and the faucet reader work unchanged.
- Mapping: `debug.angor.test_api`→`ANGOR_TEST_API`, `.network`→`ANGOR_NETWORK`,
  `.indexer_url`→`ANGOR_INDEXER_URL`, `.relay_urls`→`ANGOR_RELAY_URLS`,
  `.faucet_url`→`ANGOR_FAUCET_BASE_URL`, `.profile`→`ANGOR_PROFILE`.
- Profile isolation: `ProfileNameResolver` now falls back to the `ANGOR_PROFILE` env var
  when no `--profile` arg is present. `AndroidTestHost` also supports `pm clear`.
- Props persist until reboot; `AndroidTestHost` resets unset ones to empty each launch so
  stale values don't leak between runs.

### 4. Mobile-variant assertions/timing

- `FindProjectsViewModel`: `PageSize` 4 on mobile vs 12 desktop; `LoadMore` batches via
  `ApplicationIdle` dispatch on mobile (observable "loading" windows that don't exist on
  desktop).
- SectionPanel visibility model (views never detach) vs desktop ContentControl swap —
  selectors/waits may need mobile variants.

### 5. Security flag (fix while here) — DONE

The Android automation server is now gated like desktop: it only starts when
`ANGOR_TEST_API=1` (populated from `debug.angor.test_api` via DebugTestConfig) and binds
to loopback only — `adb forward` connects to device loopback, so the manual smoke check
becomes:

```
adb shell setprop debug.angor.test_api 1
adb shell am force-stop io.angor.app
adb shell monkey -p io.angor.app 1
adb forward tcp:18721 tcp:18721
curl http://127.0.0.1:18721/health
```

## Build/deploy reference (from AGENTS.md)

```bash
# macOS needs JavaSdkDirectory; on Windows adjust accordingly
dotnet build src/design/App.Android/App.Android.csproj -t:Install -f net9.0-android -c Debug -p:AndroidAttachDebugger=false
adb shell monkey -p io.angor.app 1
```

UAT must be Debug (automation server is `#if DEBUG`). Tests run against signet with real
transactions (1-3 min each). Skip BigFundTest/BigInvestTest for routine validation.

## Key files

| File | Role |
|---|---|
| `src/design/App/Automation/AutomationServer.cs` | HTTP server; Android path lines 48-72; `GetMainWindow` ~844 |
| `src/design/App/Automation/AutomationFlows.cs` | Flow handlers; `RequireWindowAsync`/`GetMainWindow` ~1583-1591; `NavigateToAsync` ~1518 |
| `src/design/App/App.axaml.cs` | Server startup (66-69); lifetime split (71-90) |
| `src/design/App.Test.Uat/Helpers/TestProcessHost.cs` | Desktop process host — template for AndroidTestHost |
| `src/design/App.Test.Uat/Helpers/TestAutomationClient.cs` | HTTP client wrapper (reusable as-is) |
| `src/design/App.Android/MainActivity.cs` | Entry point; PerfTabReceiver precedent (~190-205) |
| `src/design/App/Composition/CompositionRoot.cs` | Reads ANGOR_* env vars — needs Android-side equivalent |

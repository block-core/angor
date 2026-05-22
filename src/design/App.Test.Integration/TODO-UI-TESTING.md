# TODO: Real-Window UI Testing

## Problem

The current integration tests use `Avalonia.Headless.XUnit` which doesn't reliably trigger code-behind event handlers. Specifically, `RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button))` bubbles through the visual tree but the receiving view's `DataContext` is often null in headless mode, causing handlers to silently return without executing.

### Affected UI paths not covered by integration tests

- **FeeSelectionPopup**: rendering, preset selection, custom fee input, cancel/confirm flow
- **RecoveryModalsView**: `OnButtonClick` routing to `ProcessRecoveryConfirmAsync` / `ProcessReleaseConfirmAsync` / `ProcessClaimPenaltyAsync`
- **InvestmentDetailView**: `ConfirmInvestmentButton` and `CancelInvestmentButton` code-behind handlers
- **Shell modal orchestration**: `ShowModal()` / `HideModal()` transitions, backdrop click close

### Current workaround

Integration tests bypass the view layer and call ViewModel methods directly:
- `portfolioVm.ConfirmInvestmentAsync(investment)` instead of clicking `ConfirmInvestmentButton`
- `portfolioVm.ExecuteRecoveryAsync(investment, feeRate)` instead of the 3-click recovery UI flow

This validates the full SDK round-trip but leaves the view-layer wiring untested.

## Implemented direction

The repository now has the start of a real-window automation path built around one process per profile.

### Current implementation

- `src/design/App/Automation/AutomationServer.cs`
  Starts inside the real app process when `ANGOR_TEST_API=1` and `ANGOR_TEST_API_PORT` are set.
- `src/design/App/Automation/AutomationFlows.cs`
  Runs higher-level flows inside the app process on `Dispatcher.UIThread`, so the real visual tree, modal system, and ViewModels are used.
- `src/design/App.Test.Integration/Helpers/TestProcessHost.cs`
  Launches a dedicated `App.Desktop` process per profile and keeps it alive for the duration of the test.
- `src/design/App.Test.Integration/Helpers/TestAutomationClient.cs`
  Calls the automation server over localhost HTTP.
- `src/design/App.Test.Integration/BigFundTest.cs`
  New per-process orchestration test for the large fund scenario.
- `src/design/App.Test.Integration/BigInvestTest.cs`
  New per-process orchestration test for the large investment scenario.

### Key architecture decisions

- The automation code lives in shared `src/design/App/Automation/` so the same server model can later be reused by Desktop and Android.
- Communication uses a lightweight localhost HTTP protocol served from a `TcpListener`, not Avalonia headless APIs.
- Each profile runs in its own real app process instead of swapping profiles inside one headless process.
- The two large integration tests now orchestrate via process hosts and automation client calls instead of directly manipulating headless windows.

### Why this direction

- Real `DataContext` binding for code-behind handlers
- Real modal orchestration and routed events
- Isolation between profiles and between long-running flows
- Better future portability to Docker/Xvfb and Android emulator forwarding

## Proposed solutions

### Option A: In-process test API (implemented direction)

Build a test automation API that the app exposes when launched in test mode. An external test process launches the real app and communicates with it over HTTP on localhost to query the visual tree and trigger actions.

#### Architecture

```
Test Process (xUnit)                App Process (real Avalonia UI)
    |                                    |
    |-- GET /controls?name=ConfirmBtn -->|  (query visual tree)
    |<-- { Name, IsVisible, IsEnabled } -|
    |                                    |
    |-- POST /click { name: "..." } ---->|  (raise click on UI thread)
    |<-- { Success: true } -------------|
    |                                    |
    |-- GET /viewmodel/portfolio ------->|  (inspect VM state)
    |<-- { FundedProjects: 2, ... } ----|
```

#### Components

1. **`TestAutomationServer`** — embedded in the shared app project, started conditionally via environment variables. Runs a lightweight HTTP server over `TcpListener`. Dispatches commands to the UI thread via `Dispatcher.UIThread.InvokeAsync`.

2. **`TestAutomationClient`** — shared helper consumed by the test project. Provides a typed API.

    ```csharp
    var client = new TestAutomationClient("pipe://angor-test");

    // Find and interact with controls
    var btn = await client.FindControlAsync<ButtonInfo>("ConfirmInvestmentButton");
    Assert.True(btn.IsVisible);
    await client.ClickAsync("ConfirmInvestmentButton");

    // Wait for conditions
    await client.WaitForAsync(c => c.FindControl("FeeConfirmButton")?.IsVisible == true);

    // Inspect ViewModel state
    var portfolio = await client.GetViewModelAsync<PortfolioState>("PortfolioViewModel");
    Assert.Equal(3, portfolio.InvestmentStep);
    ```

3. **Control lookup** — walks `Window.GetVisualDescendants()` on the UI thread, returns serializable descriptors.

4. **Action dispatch** — all mutations run on `Dispatcher.UIThread` so `DataContext`, event routing, and modal orchestration work exactly as in production.

5. **Composite flow endpoints** — higher-level endpoints in `AutomationFlows.cs` encapsulate multi-step flows such as wallet creation, project deployment, investing, approval, stage claims, and recovery/release actions. This avoids hundreds of tiny round trips from the test process.

#### Advantages over headless

- Real `DataContext` binding — code-behind handlers execute correctly
- Real modal rendering — `FeeSelectionPopup.ShowAsync` works end-to-end
- Real event routing — `AddHandler(Button.ClickEvent, ...)` receives bubbled events
- No `AsyncLocal<TestContext>` cascade bug — each test launches a fresh app process
- Tests validate actual user-facing behavior, not just ViewModel logic

#### Implementation steps

1. Keep automation code in shared `App/Automation/`
2. Start the app with `ANGOR_TEST_API=1` and `ANGOR_TEST_API_PORT=<port>`
3. Launch one real app process per profile and keep it alive for the test duration
4. Drive the two big integration tests through the automation client
5. CI: run the desktop app under `Xvfb` on Linux runners

### Option B: Real-window in-process (simpler, limited)

Use `AppBuilder` with a real platform backend instead of headless, running in the same process as the tests. Simpler to set up but still subject to threading issues and doesn't fully replicate production conditions.

1. **CI display server**: Use `Xvfb` on Linux CI runners (already available in the Docker distro runners) or a virtual display on Windows
2. **`AppBuilder.Configure<App>()`** with a real platform backend, or use `StartWithClassicDesktopLifetime` in a test harness
3. **Test scope**: Only the UI paths listed below need real-window tests. The SDK round-trip tests can stay headless
4. **Isolation**: Each test should create and dispose its own `Window` to avoid cross-test state leakage

### Specific tests to add

| Test | What it validates |
|------|-------------------|
| `ConfirmInvestmentButton_AdvancesToStep3` | Click button -> `PortfolioViewModel.ConfirmInvestmentAsync` is called -> Step changes to 3 |
| `RecoverFundsButton_ShowsFeePopup_AndCompletes` | Click Recover -> Confirm modal -> FeeSelectionPopup appears -> Confirm fee -> recovery completes |
| `FeeSelectionPopup_PresetsAndCustomFee` | Preset selection, custom fee input validation, cancel returns null |
| `CancelInvestmentButton_CancelsInvestment` | Click cancel -> investment status changes |

### References

- Avalonia headless testing docs: https://docs.avaloniaui.net/docs/concepts/headless/headless-xunit
- `AsyncLocal<TestContext>` cascade bug: after ~19min of continuous headless execution, `KeyValueStorage` becomes inaccessible for subsequent tests
- Current headless workarounds in `TestHelpers.ClickRecoveryFlowAsync` and `ConfirmApprovedInvestmentAsync`

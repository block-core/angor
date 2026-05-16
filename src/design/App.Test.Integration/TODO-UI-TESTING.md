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

## Proposed solutions

### Option A: In-process test API (preferred)

Build a test automation API that the app exposes when launched in test mode. An external test process launches the real app and communicates with it over IPC (named pipe, HTTP on localhost, or similar) to query the visual tree and trigger actions.

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

1. **`TestAutomationServer`** — embedded in the app, started conditionally (e.g. `--test-api` flag or environment variable). Runs a lightweight HTTP/named-pipe listener. Dispatches commands to the UI thread via `Dispatcher.UIThread.InvokeAsync`.

2. **`TestAutomationClient`** — NuGet package or shared library consumed by the test project. Provides a typed API:

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

3. **Control lookup** — walks `Window.GetVisualDescendants()` on the UI thread, returns serializable descriptors (Name, AutomationId, Type, IsVisible, IsEnabled, DataContext type, bounds).

4. **Action dispatch** — all mutations run on `Dispatcher.UIThread` so `DataContext`, event routing, and modal orchestration work exactly as in production.

#### Advantages over headless

- Real `DataContext` binding — code-behind handlers execute correctly
- Real modal rendering — `FeeSelectionPopup.ShowAsync` works end-to-end
- Real event routing — `AddHandler(Button.ClickEvent, ...)` receives bubbled events
- No `AsyncLocal<TestContext>` cascade bug — each test launches a fresh app process
- Tests validate actual user-facing behavior, not just ViewModel logic

#### Implementation steps

1. Add `App.TestApi` project with `TestAutomationServer` (named pipe listener)
2. Wire it into `App.Desktop` behind a `--test-api` launch flag
3. Add `TestAutomationClient` helper class to the test project
4. Convert the affected UI tests (see table below) to use the client
5. CI: launch app with `--test-api` + `Xvfb` on Linux, run tests against it

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

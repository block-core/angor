# App.Test.Integration Testing Guidelines

## Overview

The `App.Test.Integration` project contains end-to-end integration tests for the Angor application. These tests simulate real user interactions with the UI to verify the complete functionality of the application.

## General Guidelines

1. **Stay High-Level**: Tests should simulate user interactions by loading view models, populating text boxes, and clicking buttons to trigger actions.

2. **Verify UI State**: Include assertions to check that the correct values are set on UI elements such as text boxes, buttons, and popups.

3. **Impersonate a User**: Tests should mimic how a real user would interact with the app, including navigating through different sections and performing actions.

## What We Are Testing

The current test suite covers the following scenarios:

1. **CreateProjectTest**: Tests the complete flow of creating and deploying an investment-type project, including wallet creation, funding, and project setup.

2. **FundAndRecoverTest**: Tests the flow of creating a fund-type project, investing in it, founder approval, and recovering funds.

3. **MultiFundClaimAndRecoverTest**: Tests multiple investors investing in a fund project, founder claiming stages, and investors recovering funds.

4. **MultiInvestClaimAndRecoverTest**: Tests multiple investors investing in an investment project, founder claiming stages, and investors claiming remaining stages.

5. **SendToSelfTest**: Tests the complete wallet lifecycle, including wallet creation, funding, sending transactions, and verifying balances.

6. **SmokeTest**: Basic tests to verify the headless platform and UI control finding functionality.

7. **InvestAndRecoverTest**: Tests the investment and recovery flow for a single investor in an invest-type project.

8. **InvestmentCancellationTest**: Tests cancelling an investment before the founder approves it.

9. **FindProjectsPanelTests**: Tests the Find Projects section panel visibility state machine (list → detail → invest page transitions), project card rendering, wallet selector, and invoice flow.

10. **FindProjectsInvoiceFlowTest**: Tests the complete 1-click invest invoice flow end-to-end, including faucet payment and payment detection.

11. **OneClickInvestLightningTest**: Tests the Lightning invoice tab state machine in the invest flow (Boltz swap creation, tab switching, error isolation).

12. **OneClickInvestOnChainTest**: Tests the on-chain invoice tab state machine (address generation, monitoring, tab switch race conditions).

13. **InvestModalsViewFixesTest**: Smoke test verifying InvestPageViewModel fixes: HasError binding, ShowInvoice immediate UX, tab-driven labels, and defensive error labeling.

14. **WalletImportAndProjectScanTest**: Tests importing an existing wallet via seed words and scanning for projects associated with it.

## What Can Still Be Tested

1. **Additional UI Validations**: More detailed checks on UI elements such as labels, icons, and layouts.

2. **Error Handling**: Tests for error scenarios such as invalid inputs, network failures, and edge cases.

3. **Performance**: Tests to measure the performance of various operations and ensure they meet acceptable thresholds.

4. **Accessibility**: Tests to ensure the application is accessible, including keyboard navigation and screen reader compatibility.

5. **Localization**: Tests to verify that the application works correctly with different languages and locales.

6. **Additional User Flows**: More complex user flows that involve multiple steps and interactions.

## Interaction Hierarchy (UI > ViewModel > SDK)

Tests should interact with the application at the **highest possible level**. The preferred order is:

1. **UI-level** (best): Use `TestHelpers.Click*Async` helpers, find controls by `AutomationId`, click buttons, fill text boxes. This most closely simulates a real user.

2. **ViewModel-level** (acceptable): Call ViewModel methods and read ViewModel properties. This is fine for:
   - Checking state (e.g., `investVm.CurrentScreen.Should().Be(...)`)
   - Triggering actions that don't have a corresponding UI click helper yet
   - Loading/refreshing data via `Load*Async` methods (polling for state changes)

3. **SDK service-level** (last resort): Directly call `IWalletAppService`, `IProjectAppService`, `IInvestmentAppService`, etc. **Avoid this whenever possible.** If a test must call an SDK service directly, add a comment explaining **why** — for example:
   ```csharp
   // DIRECT SDK CALL: No ViewModel exposes this data — we need the raw
   // ProjectDto to validate stage configuration persisted correctly.
   var projectDto = await projectAppService.GetFounderProjects(walletId);
   ```

### ViewModel Construction

**Never construct ViewModels with `new`** in tests. Instead, navigate through the UI to reach the desired ViewModel:

```csharp
// ✗ Wrong — bypasses navigation and DI wiring
var vm = new InvestPageViewModel(project, walletAppService, ...);

// ✓ Correct — uses the app's navigation and factory
window.NavigateToSection("Find Projects");
var findVm = GetFindProjectsViewModel(window);
findVm.OpenProjectDetail(project);
findVm.OpenInvestPage();
var investVm = findVm.InvestPageViewModel;
```

### Diagnostic SDK Calls

Some tests call SDK services in diagnostic/retry helpers to log intermediate state for debugging test failures. These are acceptable but must be clearly marked:

```csharp
// DIAGNOSTIC SDK CALL: Logs the raw build result to help diagnose
// why the UI-level retry is failing. Not part of the test flow.
var buildResult = await investmentAppService.BuildRecoveryTransaction(...);
Log(buildResult.IsSuccess ? "Build OK" : $"Build failed: {buildResult.Error}");
```

## Best Practices

1. **Use AutomationProperties.AutomationId**: Ensure UI controls have AutomationId set for reliable identification in tests.

2. **Modularize Helper Methods**: Create reusable helper methods for common tasks such as navigating to sections, finding controls, and performing actions.

3. **Log Actions**: Use logging to track the progress of tests and aid in debugging. Prefer injected `ILogger`-based logging in production and shared code so that log output is structured and routed through the standard logging pipeline. If you use `Console.WriteLine` for quick diagnostic output during investigation, treat it as temporary — remove it before committing. However, if a diagnostic log turns out to be genuinely useful (e.g., it logs state that would help diagnose future failures), convert it to an `ILogger` call and keep it rather than discarding it.

4. **Handle Asynchronous Operations**: Use async/await and appropriate timeouts to handle asynchronous operations such as network requests and UI updates.

5. **Clean Up Resources**: Ensure resources such as windows and view models are properly disposed of after tests to avoid memory leaks.

6. **Use Controls to Check Item Status**: After performing an action, find the relevant UI control and inspect its state (text, visibility, enabled, item count, etc.) to verify the operation succeeded. Do not rely solely on ViewModel properties — always confirm through the actual control tree that the UI reflects the expected state.

7. **Add Diagnostic Logs Before Assertions**: Before any assertion that could fail, log the current state of the relevant data (e.g., item counts, property values, control text). This makes it possible to understand *why* a test failed from the log output alone, without needing to reproduce and debug interactively.

## Example Test Structure

```csharp
[AvaloniaFact]
public async Task ExampleTest()
{
    using var profileScope = TestProfileScope.For(nameof(ExampleTest));
    Log("========== STARTING ExampleTest ==========");

    // Arrange: Boot the full app with ShellView
    var window = TestHelpers.CreateShellWindow();
    var shellVm = window.GetShellViewModel();

    // Act: Perform user actions
    window.NavigateToSection("SectionName");
    await Task.Delay(500);
    Dispatcher.UIThread.RunJobs();

    // Assert: Verify UI state
    var control = await window.WaitForControl<ControlType>("AutomationId", Timeout);
    control.Should().NotBeNull("Control should be visible");

    // Cleanup: close window
    window.Close();
    Log("========== ExampleTest PASSED ==========");
}
```

## Running Tests

To run the integration tests, use the following command:

```bash
dotnet test src/design/App.Test.Integration/App.Test.Integration.csproj
```

## Debugging Tests

- Use logging to track the progress of tests and identify issues.
- Run tests with detailed logging to see the output:

```bash
dotnet test src/design/App.Test.Integration/App.Test.Integration.csproj --logger "console;verbosity=detailed"
```

## Contributing

When adding new tests, follow the existing patterns and guidelines to ensure consistency and maintainability. Add detailed comments and logging to make the tests easy to understand and debug.
# SmokeTest

## Purpose

Validates that the Avalonia headless test infrastructure works correctly — the platform can boot, windows can be shown, and controls can be found by `AutomationProperties.AutomationId`.

## Tests

### `Headless_Platform_Can_Show_Window_And_Find_Control_By_AutomationId`

| | |
|---|---|
| **Type** | Smoke |
| **Network** | None |
| **Duration** | < 1s |

**Verifies**: A `Button` with an explicit `AutomationProperties.AutomationId` can be found in the visual tree using `FindByAutomationId<T>()` after the window is shown.

**Steps**:
1. Create a `Button` with `AutomationId = "TestClickButton"` and `Content = "Click Me"`.
2. Place it in a `Window` and show the window.
3. Search the visual tree for a `Button` with `AutomationId = "TestClickButton"`.
4. Assert the button is found and its `Content` matches.

---

### `Global_Automation_Styles_Assign_AutomationId_From_Button_Content`

| | |
|---|---|
| **Type** | Smoke |
| **Network** | None |
| **Duration** | < 1s |

**Verifies**: The global `Automation.axaml` styles (loaded via `Theme.axaml`) auto-assign `AutomationId` from a button's text `Content` by converting it to PascalCase (e.g., `"Save Project"` becomes `"SaveProject"`).

**Steps**:
1. Create a `Button` with `Content = "Save Project"` and **no** explicit `AutomationId`.
2. Place it in a `Window` and show the window (triggers style application).
3. Search for a `Button` with `AutomationId = "SaveProject"`.
4. Assert the button is found (proves the `ContentToAutomationId` converter ran).

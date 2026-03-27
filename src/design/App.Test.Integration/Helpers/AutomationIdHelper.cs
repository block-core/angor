using Avalonia;
using Avalonia.Automation;
using Avalonia.VisualTree;

namespace App.Test.Integration.Helpers;

/// <summary>
/// Extension methods for finding Avalonia controls by AutomationProperties.AutomationId.
/// Uses the same identifiers that Appium would use, so tests written with these helpers
/// are directly portable to Appium end-to-end tests in the future.
/// </summary>
public static class AutomationIdHelper
{
    /// <summary>
    /// Finds the first control of type <typeparamref name="T"/> with the specified AutomationId
    /// in the visual tree rooted at <paramref name="root"/>.
    /// </summary>
    /// <typeparam name="T">The type of control to find.</typeparam>
    /// <param name="root">The root visual to search from (typically a Window).</param>
    /// <param name="automationId">The AutomationProperties.AutomationId value to match.</param>
    /// <returns>The first matching control, or null if not found.</returns>
    public static T? FindByAutomationId<T>(this Visual root, string automationId)
        where T : Visual
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);
    }

    /// <summary>
    /// Finds the first visual element with the specified AutomationId regardless of type.
    /// </summary>
    /// <param name="root">The root visual to search from (typically a Window).</param>
    /// <param name="automationId">The AutomationProperties.AutomationId value to match.</param>
    /// <returns>The first matching visual, or null if not found.</returns>
    public static Visual? FindByAutomationId(this Visual root, string automationId)
    {
        return root.GetVisualDescendants()
            .OfType<Visual>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);
    }

    /// <summary>
    /// Returns all controls of type <typeparamref name="T"/> with the specified AutomationId.
    /// Useful when multiple controls share the same id (e.g., repeated items in a list).
    /// </summary>
    public static IEnumerable<T> FindAllByAutomationId<T>(this Visual root, string automationId)
        where T : Visual
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .Where(c => AutomationProperties.GetAutomationId(c) == automationId);
    }
}

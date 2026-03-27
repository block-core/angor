using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using App.Test.Integration.Helpers;

namespace App.Test.Integration;

public class SmokeTest
{
    /// <summary>
    /// Verifies the headless platform starts, a window can be shown,
    /// and the AutomationIdHelper can find controls by their AutomationId.
    /// </summary>
    [AvaloniaFact]
    public void Headless_Platform_Can_Show_Window_And_Find_Control_By_AutomationId()
    {
        // Arrange - create a button with an explicit AutomationId
        var button = new Button { Content = "Click Me" };
        AutomationProperties.SetAutomationId(button, "TestClickButton");

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = button
        };

        // Act
        window.Show();

        // Assert - find the button via AutomationIdHelper
        var found = window.FindByAutomationId<Button>("TestClickButton");
        found.Should().NotBeNull("the button should be found by its AutomationId");
        found!.Content.Should().Be("Click Me");
    }

    /// <summary>
    /// Verifies the Automation.axaml global styles auto-assign AutomationId
    /// from button Content text (e.g., "Save Project" -> "SaveProject").
    /// </summary>
    [AvaloniaFact]
    public void Global_Automation_Styles_Assign_AutomationId_From_Button_Content()
    {
        // Arrange - button with text content, no explicit AutomationId
        var button = new Button { Content = "Save Project" };
        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = button
        };

        // Act
        window.Show();

        // Assert - the Automation.axaml style should convert "Save Project" -> "SaveProject"
        var found = window.FindByAutomationId<Button>("SaveProject");
        found.Should().NotBeNull(
            "the global Automation.axaml style should auto-assign AutomationId from button Content");
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using App.UI.Shared.Helpers;

namespace App.UI.Sections.MyProjects;

/// <summary>
/// Contains the main content sections of ManageProjectView:
/// Project ID card, Project Statistics, Next Stage + Transaction Statistics, Stages.
/// Shares DataContext (ManageProjectViewModel) with parent via inheritance.
/// Stage button clicks bubble up via RoutingStrategies.Bubble to the parent.
/// </summary>
public partial class ManageProjectContentView : UserControl
{
    public ManageProjectContentView()
    {
        InitializeComponent();

        // Stage card buttons use routed event bubbling — attach handler on the
        // ItemsControl so clicks on Claim/Spent buttons inside the DataTemplate
        // bubble up. The parent ManageProjectView catches these to open modals.
        var stagesCtrl = this.FindControl<ItemsControl>("StagesItemsControl");
        stagesCtrl?.AddHandler(Button.ClickEvent, OnStageButtonClick, RoutingStrategies.Bubble);

        // Wire copy project ID button
        // Vue: copyToClipboard(projectId) — .copy-button in ManageFunds.vue
        var copyBtn = this.FindControl<Button>("CopyProjectIdBtn");
        if (copyBtn != null)
            copyBtn.Click += (_, _) =>
            {
                if (DataContext is ManageProjectViewModel vm)
                    ClipboardHelper.CopyToClipboard(this, vm.ProjectId);
            };
    }

    /// <summary>
    /// Raised when a stage Claim or Spent button is clicked.
    /// The parent ManageProjectView subscribes to this to open the appropriate modal.
    /// </summary>
    public event System.Action<int, string>? StageButtonClicked;

    private void OnStageButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        if (btn.Classes.Contains("StageClaimBtn") && btn.Tag is int claimStageNum)
        {
            StageButtonClicked?.Invoke(claimStageNum - 1, "Claim");
            e.Handled = true;
        }
        else if (btn.Classes.Contains("StageSpentBtn") && btn.Tag is int spentStageNum)
        {
            StageButtonClicked?.Invoke(spentStageNum - 1, "Spent");
            e.Handled = true;
        }
    }
}

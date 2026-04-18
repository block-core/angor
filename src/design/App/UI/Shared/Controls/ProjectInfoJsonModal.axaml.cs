using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Controls;

/// <summary>
/// Modal that displays the full project info as formatted JSON.
/// Loaded on demand from the SDK's LiteDB cache via IProjectAppService.
/// </summary>
public partial class ProjectInfoJsonModal : UserControl, IBackdropCloseable
{
    private readonly ILogger<ProjectInfoJsonModal> _logger;
    private string _json = "";

    /// <summary>
    /// Parameterless constructor for XAML designer/loader.
    /// </summary>
    public ProjectInfoJsonModal()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ProjectInfoJsonModal>();
        WireButtons();
    }

    public ProjectInfoJsonModal(string projectId, IProjectAppService projectAppService)
    {
        InitializeComponent();
        WireButtons();
        _ = LoadJsonAsync(projectId, projectAppService);
    }

    private void WireButtons()
    {
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += OnCloseClick;

        var copyBtn = this.FindControl<Button>("CopyJsonButton");
        if (copyBtn != null) copyBtn.Click += OnCopyClick;
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    public void OnBackdropCloseRequested() { }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        ShellVm?.HideModal();
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_json)) return;

            ClipboardHelper.CopyToClipboard(this, _json);

            var copyBtn = this.FindControl<Button>("CopyJsonButton");
            if (copyBtn == null) return;

            copyBtn.Content = "Copied!";
            copyBtn.Classes.Set("CopiedState", true);

            await Task.Delay(2000);

            copyBtn.Content = "Copy JSON";
            copyBtn.Classes.Set("CopiedState", false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnCopyClick failed");
        }
    }

    private async Task LoadJsonAsync(string projectId, IProjectAppService projectAppService)
    {
        var jsonContent = this.FindControl<TextBlock>("JsonContent");
        var statusText = this.FindControl<TextBlock>("StatusText");

        try
        {
            var result = await projectAppService.GetProjectInfoJson(new ProjectId(projectId));

            if (result.IsSuccess)
            {
                _json = result.Value.Json;
                if (jsonContent != null) jsonContent.Text = _json;
                if (statusText != null) statusText.Text = $"Project: {projectId}";
            }
            else
            {
                if (jsonContent != null) jsonContent.Text = $"Failed to load project info: {result.Error}";
                if (statusText != null) statusText.Text = "Error";
            }
        }
        catch (Exception ex)
        {
            if (jsonContent != null) jsonContent.Text = $"Error: {ex.Message}";
            if (statusText != null) statusText.Text = "Error";
        }
    }
}

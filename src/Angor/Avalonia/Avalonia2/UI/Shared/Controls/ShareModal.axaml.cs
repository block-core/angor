using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared.Helpers;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Shared.Controls;

/// <summary>
/// Share Project Modal — Vue HubProjectDetail.vue lines 579-693
/// Shows project info, copy-able share link, social media grid, and email button.
/// Opened from ProjectDetailView, ManageProjectView, or ProjectCard share buttons.
/// </summary>
public partial class ShareModal : UserControl, IBackdropCloseable
{
    private readonly string _projectName;
    private readonly string _projectDescription;
    private readonly string _shareUrl;

    /// <summary>
    /// Parameterless constructor for XAML designer/loader.
    /// Not used at runtime — use the parameterized constructor instead.
    /// </summary>
    public ShareModal() : this("Sample Project", "A sample project description") { }

    public ShareModal(string projectName, string projectDescription, string? avatarUrl = null)
    {
        _projectName = projectName;
        _projectDescription = projectDescription;
        _shareUrl = $"https://angor.io/project/{projectName.ToLowerInvariant().Replace(" ", "-")}";

        InitializeComponent();

        // Set project info
        ProjectTitle.Text = _projectName;
        ProjectDescription.Text = _projectDescription;
        ShareLinkInput.Text = _shareUrl;

        // Set avatar if provided
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            try
            {
                ProjectAvatar.Source = new Avalonia.Media.Imaging.Bitmap(avatarUrl);
            }
            catch
            {
                // Avatar load failed — leave empty (white circle shows)
            }
        }

        // Wire button handlers
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += OnCloseClick;

        var copyBtn = this.FindControl<Button>("CopyButton");
        if (copyBtn != null) copyBtn.Click += OnCopyClick;

        // Social buttons + email — PointerPressed on Borders
        WireBorderClick("BtnTwitter", () => ShareOnPlatform("twitter"));
        WireBorderClick("BtnFacebook", () => ShareOnPlatform("facebook"));
        WireBorderClick("BtnLinkedIn", () => ShareOnPlatform("linkedin"));
        WireBorderClick("BtnTelegram", () => ShareOnPlatform("telegram"));
        WireBorderClick("BtnWhatsApp", () => ShareOnPlatform("whatsapp"));
        WireBorderClick("BtnReddit", () => ShareOnPlatform("reddit"));
        WireBorderClick("BtnEmail", () => ShareViaEmail());
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
        ClipboardHelper.CopyToClipboard(this, _shareUrl);

        var copyBtn = this.FindControl<Button>("CopyButton");
        if (copyBtn == null) return;

        // Change button text + color to "Copied!" for 2 seconds via CSS class
        copyBtn.Content = "Copied!";
        copyBtn.Classes.Set("CopiedState", true);

        await Task.Delay(2000);

        // Restore original
        copyBtn.Content = "Copy";
        copyBtn.Classes.Set("CopiedState", false);
    }

    /// <summary>
    /// Stub: open share URL for a social platform.
    /// In production this would call Process.Start with the platform share URL.
    /// </summary>
    private void ShareOnPlatform(string platform)
    {
        var encodedUrl = Uri.EscapeDataString(_shareUrl);
        var encodedTitle = Uri.EscapeDataString(_projectName);
        var url = platform switch
        {
            "twitter" => $"https://twitter.com/intent/tweet?url={encodedUrl}&text={encodedTitle}",
            "facebook" => $"https://www.facebook.com/sharer/sharer.php?u={encodedUrl}",
            "linkedin" => $"https://www.linkedin.com/sharing/share-offsite/?url={encodedUrl}",
            "telegram" => $"https://t.me/share/url?url={encodedUrl}&text={encodedTitle}",
            "whatsapp" => $"https://wa.me/?text={encodedTitle}%20{encodedUrl}",
            "reddit" => $"https://reddit.com/submit?url={encodedUrl}&title={encodedTitle}",
            _ => _shareUrl,
        };

        // Open in default browser
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Silently fail if browser can't be opened (visual-only prototype)
        }
    }

    private void ShareViaEmail()
    {
        var subject = Uri.EscapeDataString($"Check out {_projectName} on Angor");
        var body = Uri.EscapeDataString($"I found this project on Angor: {_shareUrl}");
        var mailto = $"mailto:?subject={subject}&body={body}";

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = mailto,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Silently fail
        }
    }

    private void WireBorderClick(string borderName, Action handler)
    {
        var border = this.FindControl<Border>(borderName);
        if (border != null)
        {
            border.PointerPressed += (_, e) =>
            {
                handler();
                e.Handled = true;
            };
        }
    }
}

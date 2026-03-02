using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Avalonia2.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep3View : UserControl
{
    public CreateProjectStep3View()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Wire avatar border click (it's a Border, not a Button, so Button.ClickEvent won't fire)
        var avatarBorder = this.FindControl<Border>("UploadAvatarButton");
        if (avatarBorder != null)
            avatarBorder.PointerPressed += (_, _) => _ = PickImageAsync(false);

        // Wire banner button click
        var bannerButton = this.FindControl<Button>("UploadBannerButton");
        if (bannerButton != null)
            bannerButton.Click += (_, _) => _ = PickImageAsync(true);
    }

    #region Image Picker

    /// <summary>
    /// Open a file picker dialog to select an image for banner or avatar.
    /// </summary>
    private async Task PickImageAsync(bool isBanner)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = isBanner ? "Select Banner Image" : "Select Profile Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" } }
            }
        });

        if (files.Count == 0) return;

        var file = files[0];
        try
        {
            await using var stream = await file.OpenReadAsync();
            // Decode bitmap off the UI thread to avoid blocking during large image loads
            var bitmap = await Task.Run(() => Bitmap.DecodeToWidth(stream, 800));

            if (isBanner)
            {
                var bannerImage = this.FindControl<Image>("BannerPreviewImage");
                if (bannerImage != null)
                {
                    bannerImage.Source = bitmap;
                    bannerImage.IsVisible = true;
                }
            }
            else
            {
                var avatarImage = this.FindControl<Image>("AvatarPreviewImage");
                var avatarIcon = this.FindControl<Control>("AvatarUploadIcon");
                if (avatarImage != null)
                {
                    avatarImage.Source = bitmap;
                    avatarImage.IsVisible = true;
                }
                if (avatarIcon != null)
                {
                    avatarIcon.IsVisible = false;
                }
            }
        }
        catch
        {
            // File read error — silently ignore for prototype
        }
    }

    #endregion

    /// <summary>
    /// Reset image previews to default state (called by parent on wizard reset).
    /// </summary>
    public void ResetVisualState()
    {
        var bannerImage = this.FindControl<Image>("BannerPreviewImage");
        if (bannerImage != null)
        {
            bannerImage.Source = null;
            bannerImage.IsVisible = false;
        }
        var avatarImage = this.FindControl<Image>("AvatarPreviewImage");
        if (avatarImage != null)
        {
            avatarImage.Source = null;
            avatarImage.IsVisible = false;
        }
        var avatarIcon = this.FindControl<Control>("AvatarUploadIcon");
        if (avatarIcon != null) avatarIcon.IsVisible = true;
    }
}

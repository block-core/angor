using System.Linq;
using Angor.Shared.Models;

namespace AngorApp.UI.Shared.Controls.ImageUploadWizard;

/// <summary>
/// Design-time sample for the ImageUploadWizard view model.
/// </summary>
public class ImageUploadWizardViewModelSample : IImageUploadWizardViewModel
{
    public bool IsUploadMode { get; set; } = true;
    public string? ImageUri { get; set; } = "https://picsum.photos/400/300";
    public IReadOnlyList<ImageServerConfig> Servers { get; } = ImageServerConfig.GetDefaultServers().AsReadOnly();
    public ImageServerConfig? SelectedServer { get; set; } = ImageServerConfig.GetDefaultServers().First();
    public string? CustomServerUrl { get; set; }
    public string? SelectedFileName { get; } = "sample-image.png";
    public long SelectedFileSize { get; } = 1024 * 512; // 512 KB
    public bool IsUploading { get; } = false;
    public string? StatusMessage { get; } = null;
    public bool IsSuccess { get; } = false;
    public bool HasSelectedFile { get; } = true;
    public bool CanUpload { get; } = true;
    public IEnhancedCommand SelectFile { get; } = null!;
    public IEnhancedCommand Upload { get; } = null!;
}

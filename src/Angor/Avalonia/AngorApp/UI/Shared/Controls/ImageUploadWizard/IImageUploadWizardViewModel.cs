using Angor.Shared.Models;

namespace AngorApp.UI.Shared.Controls.ImageUploadWizard;

/// <summary>
/// Interface for the Image Upload Wizard view model.
/// </summary>
public interface IImageUploadWizardViewModel
{
    /// <summary>
    /// Gets or sets whether the control is in upload mode (true) or URL mode (false).
    /// </summary>
    bool IsUploadMode { get; set; }

    /// <summary>
    /// Gets or sets the current image URL.
    /// </summary>
    string? ImageUri { get; set; }

    /// <summary>
    /// Gets the list of available image servers.
    /// </summary>
    IReadOnlyList<ImageServerConfig> Servers { get; }

    /// <summary>
    /// Gets or sets the selected server.
    /// </summary>
    ImageServerConfig? SelectedServer { get; set; }

    /// <summary>
    /// Gets or sets the custom server URL (when using custom server).
    /// </summary>
    string? CustomServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the selected file name.
    /// </summary>
    string? SelectedFileName { get; }

    /// <summary>
    /// Gets or sets the selected file size in bytes.
    /// </summary>
    long SelectedFileSize { get; }

    /// <summary>
    /// Gets whether an upload is currently in progress.
    /// </summary>
    bool IsUploading { get; }

    /// <summary>
    /// Gets the status message for the upload operation.
    /// </summary>
    string? StatusMessage { get; }

    /// <summary>
    /// Gets whether the last upload was successful.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets whether a file is selected for upload.
    /// </summary>
    bool HasSelectedFile { get; }

    /// <summary>
    /// Gets whether the upload button should be enabled.
    /// </summary>
    bool CanUpload { get; }

    /// <summary>
    /// Command to select a file for upload.
    /// </summary>
    IEnhancedCommand SelectFile { get; }

    /// <summary>
    /// Command to upload the selected file.
    /// </summary>
    IEnhancedCommand Upload { get; }
}

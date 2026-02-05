using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using Angor.Shared.Models;
using Avalonia.Platform.Storage;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Shared.Controls.ImageUploadWizard;

public partial class ImageUploadWizardViewModel : ReactiveValidationObject, IImageUploadWizardViewModel
{
    private readonly IImageUploadService _uploadService;
    private readonly Func<TopLevel?> _getTopLevel;
    private readonly CompositeDisposable _disposable = new();

    [Reactive] private bool isUploadMode;
    [Reactive] private string? imageUri;
    [Reactive] private ImageServerConfig? selectedServer;
    [Reactive] private string? customServerUrl;
    [Reactive] private string? selectedFileName;
    [Reactive] private long selectedFileSize;
    [Reactive] private bool isUploading;
    [Reactive] private string? statusMessage;
    [Reactive] private bool isSuccess;
    [Reactive] private bool hasSelectedFile;

    private Stream? _selectedFileStream;
    private string? _selectedContentType;

    public ImageUploadWizardViewModel(
        IImageUploadService uploadService,
        Func<TopLevel?> getTopLevel)
    {
        _uploadService = uploadService ?? throw new ArgumentNullException(nameof(uploadService));
        _getTopLevel = getTopLevel ?? throw new ArgumentNullException(nameof(getTopLevel));

        Servers = _uploadService.GetServers();
        SelectedServer = Servers.FirstOrDefault();

        // Image URL validation
        var isValidImage = this.WhenAnyValue(model => model.ImageUri)
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.MainThreadScheduler)
            .Select(uri => IsValidImageUrl(uri))
            .ObserveOn(RxApp.MainThreadScheduler);

        this.ValidationRule(x => x.ImageUri,
            isValidImage,
            isValid => isValid,
            _ => "Please enter a valid image URL (http:// or https://)").DisposeWith(_disposable);

        // Select file command
        var canSelectFile = this.WhenAnyValue(x => x.IsUploading).Select(uploading => !uploading);
        SelectFile = ReactiveCommand.CreateFromTask(ExecuteSelectFileAsync, canSelectFile).Enhance();

        // Upload command
        var canUpload = this.WhenAnyValue(
            x => x.HasSelectedFile,
            x => x.IsUploading,
            x => x.SelectedServer,
            x => x.CustomServerUrl,
            (hasFile, uploading, server, customUrl) =>
                hasFile && !uploading && server != null &&
                (!server.IsCustom || !string.IsNullOrWhiteSpace(customUrl)));

        Upload = ReactiveCommand.CreateFromTask(ExecuteUploadAsync, canUpload).Enhance();

        // Compute CanUpload property
        canUpload.Subscribe(value => this.RaisePropertyChanged(nameof(CanUpload)));
    }

    public IReadOnlyList<ImageServerConfig> Servers { get; }

    public bool CanUpload => HasSelectedFile && !IsUploading && SelectedServer != null &&
                             (!SelectedServer.IsCustom || !string.IsNullOrWhiteSpace(CustomServerUrl));

    public IEnhancedCommand SelectFile { get; }

    public IEnhancedCommand Upload { get; }

    private static bool IsValidImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true; // Empty is valid (optional)

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        return false;
    }

    private async Task ExecuteSelectFileAsync()
    {
        var topLevel = _getTopLevel();
        if (topLevel == null)
        {
            StatusMessage = "Cannot open file picker.";
            IsSuccess = false;
            return;
        }

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
        {
            StatusMessage = "File picker is not available on this platform.";
            IsSuccess = false;
            return;
        }

        var fileTypes = new FilePickerFileType[]
        {
            new("Image Files")
            {
                Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.webp", "*.svg" },
                MimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" }
            }
        };

        var options = new FilePickerOpenOptions
        {
            Title = "Select an image to upload",
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        };

        var files = await storageProvider.OpenFilePickerAsync(options);
        var file = files.FirstOrDefault();

        if (file == null)
        {
            return; // User cancelled
        }

        try
        {
            // Dispose previous stream if any
            _selectedFileStream?.Dispose();

            var properties = await file.GetBasicPropertiesAsync();
            _selectedFileStream = await file.OpenReadAsync();
            _selectedContentType = GetContentType(file.Name);

            SelectedFileName = file.Name;
            SelectedFileSize = (long)(properties.Size ?? 0);
            HasSelectedFile = true;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error reading file: {ex.Message}";
            IsSuccess = false;
            HasSelectedFile = false;
        }
    }

    private async Task ExecuteUploadAsync()
    {
        if (_selectedFileStream == null || SelectedServer == null || string.IsNullOrEmpty(SelectedFileName))
        {
            StatusMessage = "Please select a file first.";
            IsSuccess = false;
            return;
        }

        IsUploading = true;
        StatusMessage = "Uploading...";

        try
        {
            // Reset stream position
            if (_selectedFileStream.CanSeek)
            {
                _selectedFileStream.Position = 0;
            }

            var result = await _uploadService.UploadImageAsync(
                SelectedServer,
                _selectedFileStream,
                SelectedFileName,
                _selectedContentType ?? "application/octet-stream",
                CustomServerUrl);

            if (result.IsSuccess)
            {
                ImageUri = result.Value;
                StatusMessage = $"Image uploaded successfully to {SelectedServer.Name}!";
                IsSuccess = true;

                // Clear selected file after successful upload
                ClearSelectedFile();
            }
            else
            {
                StatusMessage = result.Error;
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upload error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsUploading = false;
        }
    }

    private void ClearSelectedFile()
    {
        _selectedFileStream?.Dispose();
        _selectedFileStream = null;
        _selectedContentType = null;
        SelectedFileName = null;
        SelectedFileSize = 0;
        HasSelectedFile = false;
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _selectedFileStream?.Dispose();
            _disposable.Dispose();
        }
        base.Dispose(disposing);
    }
}

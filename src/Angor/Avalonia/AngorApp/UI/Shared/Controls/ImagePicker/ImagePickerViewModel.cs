using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Disposables;
using Angor.Shared;
using Angor.Shared.Models;
using AngorApp.UI.Shared.Services.Blossom;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Serilog;

namespace AngorApp.UI.Shared.Controls.ImagePicker
{
    public partial class ImagePickerViewModel : ReactiveValidationObject, IImagePickerViewModel
    {
        private readonly CompositeDisposable disposable = new();
        private readonly IBlossomService blossomService;
        private byte[]? selectedFileBytes;
        private string? selectedContentType;

        [Reactive] private string? imageUri = "https://picsum.photos/320/200";
        [Reactive] private string? selectedFileName;
        [Reactive] private bool isUploading;
        [Reactive] private string? uploadStatus;
        [Reactive] private SettingsUrl? selectedServer;
        [Reactive] private string? customServerUrl;

        public ObservableCollection<SettingsUrl> ImageServers { get; }

        public ReactiveCommand<Unit, Unit> BrowseFile { get; }
        public ReactiveCommand<Unit, Unit> UploadFile { get; }
        public ReactiveCommand<Unit, Unit> AddCustomServer { get; }

        public ImagePickerViewModel(UIServices uiServices, IBlossomService blossomService, INetworkStorage networkStorage)
        {
            this.blossomService = blossomService;

            var validImage = this.WhenAnyValue(model => model.ImageUri)
                .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
                .Select(uri => Observable.FromAsync(() => uiServices.Validations.IsImage(uri)))
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler);
                
            this.ValidationRule(x => x.ImageUri, validImage, result => result.IsSuccess, result => $"Invalid image: {result}").DisposeWith(disposable);

            // Load image servers from settings
            var settings = networkStorage.GetSettings();
            ImageServers = new ObservableCollection<SettingsUrl>(settings.ImageServers);
            SelectedServer = ImageServers.FirstOrDefault(s => s.IsPrimary) ?? ImageServers.FirstOrDefault();

            BrowseFile = ReactiveCommand.CreateFromTask(DoBrowseFile).DisposeWith(disposable);

            var canUpload = this.WhenAnyValue(
                x => x.SelectedFileName,
                x => x.SelectedServer,
                x => x.IsUploading,
                (file, server, uploading) => file != null && server != null && !uploading);

            UploadFile = ReactiveCommand.CreateFromTask(DoUploadFile, canUpload).DisposeWith(disposable);

            var canAddServer = this.WhenAnyValue(x => x.CustomServerUrl,
                url => !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"));

            AddCustomServer = ReactiveCommand.Create(DoAddCustomServer, canAddServer).DisposeWith(disposable);
        }

        private void DoAddCustomServer()
        {
            if (string.IsNullOrWhiteSpace(CustomServerUrl))
                return;

            var url = CustomServerUrl.TrimEnd('/');

            // Don't add duplicates
            if (ImageServers.Any(s => string.Equals(s.Url.TrimEnd('/'), url, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedServer = ImageServers.First(s => string.Equals(s.Url.TrimEnd('/'), url, StringComparison.OrdinalIgnoreCase));
                CustomServerUrl = null;
                return;
            }

            var serverName = new Uri(url).Host;
            var newServer = new SettingsUrl { Name = serverName, Url = url, IsPrimary = false };
            ImageServers.Add(newServer);
            SelectedServer = newServer;
            CustomServerUrl = null;
        }

        private async Task DoBrowseFile()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null);

                if (topLevel == null)
                {
                    UploadStatus = "Cannot open file picker";
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select an image to upload",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Images")
                        {
                            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.svg" },
                            MimeTypes = new[] { "image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml" }
                        }
                    }
                });

                if (files.Count == 0)
                    return;

                var file = files[0];
                SelectedFileName = file.Name;

                await using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                selectedFileBytes = ms.ToArray();

                // Determine content type from extension
                var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                selectedContentType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    _ => "application/octet-stream"
                };

                UploadStatus = $"Selected: {file.Name} ({selectedFileBytes.Length / 1024}KB)";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error browsing for file");
                UploadStatus = $"Error: {ex.Message}";
            }
        }

        private async Task DoUploadFile()
        {
            if (selectedFileBytes == null || SelectedServer == null)
                return;

            IsUploading = true;
            UploadStatus = $"Uploading to {SelectedServer.Name ?? SelectedServer.Url}...";

            try
            {
                var result = await blossomService.Upload(
                    SelectedServer.Url,
                    selectedFileBytes,
                    selectedContentType ?? "application/octet-stream");

                if (result.IsSuccess)
                {
                    ImageUri = result.Value.Url;
                    UploadStatus = "Upload successful!";
                    SelectedFileName = null;
                    selectedFileBytes = null;
                }
                else
                {
                    UploadStatus = $"Upload failed: {result.Error}";
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Upload error");
                UploadStatus = $"Upload error: {ex.Message}";
            }
            finally
            {
                IsUploading = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            disposable.Dispose();
            base.Dispose(disposing);
        }
    }
}
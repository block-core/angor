using System.Collections.ObjectModel;
using System.Reactive;
using Angor.Shared.Models;
using ReactiveUI;

namespace AngorApp.UI.Shared.Controls.ImagePicker
{
    public class ImagePickerViewModelSample : IImagePickerViewModel
    {
        public string? ImageUri { get; set; } = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        public string? SelectedFileName { get; } = null;
        public bool IsUploading { get; } = false;
        public string? UploadStatus { get; } = null;
        public ObservableCollection<SettingsUrl> ImageServers { get; } = new()
        {
            new() { Name = "nostr.build", Url = "https://nostr.build", IsPrimary = true },
            new() { Name = "blossom.primal.net", Url = "https://blossom.primal.net", IsPrimary = false },
            new() { Name = "nostria (Blossom)", Url = "https://mibo.eu.nostria.app", IsPrimary = false },
        };
        public SettingsUrl? SelectedServer { get; set; }
        public string? CustomServerUrl { get; set; }
        public ReactiveCommand<Unit, Unit> BrowseFile { get; } = ReactiveCommand.Create(() => { });
        public ReactiveCommand<Unit, Unit> UploadFile { get; } = ReactiveCommand.Create(() => { });
        public ReactiveCommand<Unit, Unit> AddCustomServer { get; } = ReactiveCommand.Create(() => { });
    }
}
using System.Collections.ObjectModel;
using System.Reactive;
using Angor.Shared.Models;
using ReactiveUI;

namespace AngorApp.UI.Shared.Controls.ImagePicker
{
    public interface IImagePickerViewModel
    {
        string? ImageUri { get; set; }
        string? SelectedFileName { get; }
        bool IsUploading { get; }
        string? UploadStatus { get; }
        ObservableCollection<SettingsUrl> ImageServers { get; }
        SettingsUrl? SelectedServer { get; set; }
        string? CustomServerUrl { get; set; }
        ReactiveCommand<Unit, Unit> BrowseFile { get; }
        ReactiveCommand<Unit, Unit> UploadFile { get; }
        ReactiveCommand<Unit, Unit> AddCustomServer { get; }
    }
}
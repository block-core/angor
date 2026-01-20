using System.Reactive.Disposables;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Shared.Controls.ImagePicker
{
    public partial class ImagePickerViewModel : ReactiveValidationObject, IImagePickerViewModel
    {
        private readonly CompositeDisposable disposable = new();
        
        [Reactive] private string? imageUri = "https://picsum.photos/320/200";

        public ImagePickerViewModel(UIServices uiServices)
        {
            var validImage = this.WhenAnyValue(model => model.ImageUri)
                .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
                .Select(uri => Observable.FromAsync(() => uiServices.Validations.IsImage(uri)))
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler);
                
            this.ValidationRule(x => x.ImageUri, validImage, result => result.IsSuccess, result => $"Invalid image: {result}").DisposeWith(disposable);
        }

        protected override void Dispose(bool disposing)
        {
            disposable.Dispose();
            base.Dispose(disposing);
        }
    }
}
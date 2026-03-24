using System.Reactive.Disposables;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{

    public class ProjectImagesViewModel : ReactiveObject, IHaveTitle, IDisposable, IProjectImagesViewModel
    {
        private readonly CompositeDisposable disposable = new();

        public ProjectImagesViewModel(IProjectProfile newProject, IImagePicker imagePicker)
        {
            NewProject = newProject;
            
            PickBanner = ReactiveCommand.CreateFromTask(imagePicker.PickImage).Enhance().DisposeWith(disposable);
            PickAvatar = ReactiveCommand.CreateFromTask(imagePicker.PickImage).Enhance().DisposeWith(disposable);
            
            PickAvatar.Values().Select(uri => uri.ToString()).BindTo(NewProject, x => x.AvatarUri).DisposeWith(disposable);
            PickBanner.Values().Select(uri => uri.ToString()).BindTo(NewProject, x => x.BannerUri).DisposeWith(disposable);
        }

        public IEnhancedCommand<Maybe<Uri>> PickBanner { get; }
        public IEnhancedCommand<Maybe<Uri>> PickAvatar { get; }

        public IProjectProfile NewProject { get; }

        public IObservable<string> Title => Observable.Return("Project Images");

        public void Dispose()
        {
            disposable.Dispose();
            PickBanner.Dispose();
            PickAvatar.Dispose();
        }
    }

    public interface IImagePicker
    {
        Task<Maybe<Uri>> PickImage();
    }
}
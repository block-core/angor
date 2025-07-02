using System.Reactive.Disposables;
using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.Sections.Founder.CreateProject.Profile;
using AngorApp.Sections.Founder.CreateProject.Stages;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModel : ReactiveValidationObject, ICreateProjectViewModel
{
    private readonly CompositeDisposable disposable = new();

    public CreateProjectViewModel()
    {
        Create = ReactiveCommand.Create(() => { }, this.IsValid()).Enhance().DisposeWith(disposable);
        StagesViewModel = new StagesViewModel().DisposeWith(disposable);
        FundingStructureViewModel = new FundingStructureViewModel().DisposeWith(disposable);
        ProfileViewModel = new ProfileViewModel().DisposeWith(disposable);
        
        this.ValidationRule(StagesViewModel.IsValid, b => b, _ => "Stages are not valid").DisposeWith(disposable);
        this.ValidationRule(FundingStructureViewModel.IsValid, b => b, _ => "Funding structures not valid").DisposeWith(disposable);
        this.ValidationRule(ProfileViewModel.IsValid, b => b, _ => "Profile not valid").DisposeWith(disposable);
    }

    public IStagesViewModel StagesViewModel { get; }
    public IProfileViewModel ProfileViewModel { get; }
    public IFundingStructureViewModel FundingStructureViewModel { get; }
    public IEnhancedCommand Create { get; }
    public IObservable<bool> IsValid => this.IsValid();

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.Sections.Founder.CreateProject.Preview;
using AngorApp.Sections.Founder.CreateProject.Profile;
using AngorApp.Sections.Founder.CreateProject.Stages;
using AngorApp.Sections.Shell;
using AngorApp.UI.Controls.Common;
using AngorApp.UI.Services;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModel : ReactiveValidationObject, ICreateProjectViewModel, IHaveHeader
{
    private readonly IProjectAppService projectAppService;
    private readonly CompositeDisposable disposable = new();

    public CreateProjectViewModel(IWallet wallet, UIServices uiServices, IProjectAppService projectAppService)
    {
        this.projectAppService = projectAppService;
        StagesViewModel = new StagesViewModel().DisposeWith(disposable);
        FundingStructureViewModel = new FundingStructureViewModel().DisposeWith(disposable);
        ProfileViewModel = new ProfileViewModel().DisposeWith(disposable);

        this.ValidationRule(StagesViewModel.IsValid, b => b, _ => "Stages are not valid").DisposeWith(disposable);
        this.ValidationRule(FundingStructureViewModel.IsValid, b => b, _ => "Funding structures not valid").DisposeWith(disposable);
        this.ValidationRule(ProfileViewModel.IsValid, b => b, _ => "Profile not valid").DisposeWith(disposable);

        Create = ReactiveCommand.CreateFromTask(() =>
        {
            var feerateSelector = new FeerateSelectionViewModel(uiServices);
            var feerate = uiServices.Dialog.ShowAndGetResult(feerateSelector, "Select the feerate", f => f.IsValid, viewModel => viewModel.Feerate!.Value);
            return feerate.ToResult("Choosing a feerate is mandatory")
                .Bind(fr => DoCreateProject(wallet, this.ToDto(), fr))
                .TapError(() => uiServices.NotificationService.Show("An error occurred while creating the project. Please try again.", "Failed to create project"));
        }, IsValid).Enhance();
    }

    public IEnhancedCommand<Result<string>> Create { get; }

    private Task<Result<string>> DoCreateProject(IWallet wallet, CreateProjectDto dto, long feeRate)
    {
        return projectAppService.CreateProject(wallet.Id.Value, feeRate, dto);
    }
    
    public IObservable<bool> IsValid => this.IsValid();
    public IStagesViewModel StagesViewModel { get; }
    public IProfileViewModel ProfileViewModel { get; }
    public IFundingStructureViewModel FundingStructureViewModel { get; }

    public object? Header => new PreviewHeaderViewModel(this);

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.Sections.Founder.CreateProject.Preview;
using AngorApp.Sections.Founder.CreateProject.Profile;
using AngorApp.Sections.Founder.CreateProject.Stages;
using AngorApp.Sections.Shell;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModel : ReactiveValidationObject, ICreateProjectViewModel, IHaveHeader
{
    private readonly CompositeDisposable disposable = new();

    public CreateProjectViewModel(IInvestmentAppService investmentAppService, IWallet wallet)
    {
        Create = ReactiveCommand.Create(() => investmentAppService.CreateProject(wallet.Id.Value, 1000, ToProject()), this.IsValid()).Enhance().DisposeWith(disposable);
        StagesViewModel = new StagesViewModel().DisposeWith(disposable);
        FundingStructureViewModel = new FundingStructureViewModel().DisposeWith(disposable);
        ProfileViewModel = new ProfileViewModel().DisposeWith(disposable);

        this.ValidationRule(StagesViewModel.IsValid, b => b, _ => "Stages are not valid").DisposeWith(disposable);
        this.ValidationRule(FundingStructureViewModel.IsValid, b => b, _ => "Funding structures not valid").DisposeWith(disposable);
        this.ValidationRule(ProfileViewModel.IsValid, b => b, _ => "Profile not valid").DisposeWith(disposable);
    }

    private CreateProjectDto ToProject()
    {
        return new CreateProjectDto
        {
            //Nostr profile
            ProjectName = ProfileViewModel.ProjectName,
            Description = ProfileViewModel.Description,
            AvatarUri = ProfileViewModel.AvatarUri,
            BannerUri = ProfileViewModel.BannerUri,
            WebsiteUri = ProfileViewModel.WebsiteUri,
            Nip57 = null,//TODO: Map result...
            Nip05 = null,//TODO: Map result...
            Lud16 = null,//TODO: Map result...
            
            //Project information
            Sats = FundingStructureViewModel.Sats!.Value,
            StartDate = FundingStructureViewModel.StartDate,
            ExpiryDate = FundingStructureViewModel.ExpiryDate,
            EndDate = FundingStructureViewModel.EndDate,
            PenaltyDays = FundingStructureViewModel.PenaltyDays!.Value,
            TargetAmount = new Amount(FundingStructureViewModel.Sats!.Value),
            Stages = StagesViewModel.Stages.Select(stage => new CreateProjectStageDto(DateOnly.FromDateTime(stage.ReleaseDate!.Value.Date), stage.Percent!.Value / 100)),
        };
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

    public object? Header => new PreviewHeaderViewModel(this);
}
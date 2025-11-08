using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using AngorApp.Flows;
using AngorApp.Flows.CreateProject;
using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.Sections.Founder.CreateProject.Preview;
using AngorApp.Sections.Founder.CreateProject.Profile;
using AngorApp.Sections.Founder.CreateProject.Stages;
using AngorApp.Sections.Shell;
using AngorApp.UI.Controls.Common;
using Microsoft.Extensions.Logging;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using System.Reactive.Disposables;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModel : ReactiveValidationObject, ICreateProjectViewModel, IHaveHeader
{
    private readonly IProjectAppService projectAppService;
    private readonly CompositeDisposable disposable = new();
    private readonly ILogger<CreateProjectViewModel> logger;

    public CreateProjectViewModel(IWallet wallet, CreateProjectFlow.ProjectSeed projectSeed, UIServices uiServices, IProjectAppService projectAppService, ILogger<CreateProjectViewModel> logger, INetworkConfiguration networkConfiguration)
    {
        this.projectAppService = projectAppService;
        this.logger = logger;
        FundingStructureViewModel = new FundingStructureViewModel(networkConfiguration).DisposeWith(disposable);
        var endDateChanges = FundingStructureViewModel.WhenAnyValue(x => x.FundingEndDate);
        StagesViewModel = new StagesViewModel(endDateChanges, uiServices, networkConfiguration).DisposeWith(disposable);
        ProfileViewModel = new ProfileViewModel(projectSeed, uiServices).DisposeWith(disposable);

        StagesViewModel.LastStageDate
            .Select(date => date?.AddDays(60))
            .Subscribe(date => FundingStructureViewModel.ExpiryDate = date)
            .DisposeWith(disposable);

        this.ValidationRule(FundingStructureViewModel.IsValid, b => b, _ => "Funding structures not valid").DisposeWith(disposable);
        this.ValidationRule(ProfileViewModel.IsValid, b => b, _ => "Profile not valid").DisposeWith(disposable);
        this.ValidationRule(StagesViewModel.IsValid, b => b, _ => "Stages are not valid").DisposeWith(disposable);

        Create = ReactiveCommand.CreateFromTask(() =>
        {
            var feerateSelector = new FeerateSelectionViewModel(uiServices);
            var feerate = uiServices.Dialog.ShowAndGetResult(feerateSelector, "Select the feerate", f => f.IsValid, viewModel => viewModel.Feerate!.Value);
            return feerate.ToResult("Choosing a feerate is mandatory")
                .Bind(fr => DoCreateProject(wallet, this.ToDto(), fr))
                .TapError(error =>
                {
                    logger.LogDebug("[CreateProject] Failed to create project: {Error}\nWalletId: {WalletId}, Dto: {@Dto}", error, wallet.Id.Value, this.ToDto());
                    uiServices.NotificationService.Show("An error occurred while creating the project. Please try again.", "Failed to create project");
                });
        }, IsValid).Enhance();
    }

    public IEnhancedCommand<Result<string>> Create { get; }

    private async Task<Result<string>> DoCreateProject(IWallet wallet, CreateProjectDto dto, long feeRate)
    {
        var result = await projectAppService.CreateProject(wallet.Id.Value, feeRate, dto);
        if(result.IsSuccess)
            return Result.Success(result.Value.TransactionId); //TODO Jose, need to fix this when the changes are implemented in the UI
        logger.LogDebug("[CreateProject] Service returned failure: {Error}\nWalletId: {WalletId}, Dto: {@Dto}", result.Error, wallet.Id.Value, dto);
        return Result.Failure<string>(result.Error);
    }
    
    public IObservable<bool> IsValid => this.IsValid();
    public IStagesViewModel StagesViewModel { get; }
    public IProfileViewModel ProfileViewModel { get; }
    public IFundingStructureViewModel FundingStructureViewModel { get; }

    public object Header => new PreviewHeaderViewModel(this);

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using AngorApp.UI.Flows.CreateProject;
using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Sections.Founder.CreateProject.Preview;
using AngorApp.UI.Sections.Founder.CreateProject.Profile;
using AngorApp.UI.Sections.Founder.CreateProject.Stages;
using AngorApp.UI.Shared.Controls.Common;
using AngorApp.UI.Shared.Services;
using AngorApp.UI.TransactionDrafts;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using AngorApp.UI.TransactionDrafts.DraftTypes.Base;
using Microsoft.Extensions.Logging;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using System.Reactive.Disposables;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Sections.Founder.CreateProject;

public class CreateProjectViewModel : ReactiveValidationObject, ICreateProjectViewModel
{
    private readonly IProjectAppService projectAppService;
    private readonly IFounderAppService founderAppService;
    private readonly CompositeDisposable disposable = new();
    private readonly ILogger<CreateProjectViewModel> logger;
    private readonly IWallet wallet;
    private readonly UIServices uiServices;
    private readonly ProjectSeedDto projectSeed;

    public CreateProjectViewModel(
        IWallet wallet,
        ProjectSeedDto projectSeed,
        UIServices uiServices,
        IProjectAppService projectAppService,
        IFounderAppService founderAppService,
        ILogger<CreateProjectViewModel> logger)
    {
        this.projectAppService = projectAppService;
        this.founderAppService = founderAppService;
        this.logger = logger;
        this.wallet = wallet;
        this.uiServices = uiServices;
        this.projectSeed = projectSeed;

        FundingStructureViewModel = new FundingStructureViewModel(uiServices).DisposeWith(disposable);
        var endDateChanges = FundingStructureViewModel.WhenAnyValue(x => x.FundingEndDate);
        StagesViewModel = new StagesViewModel(endDateChanges, uiServices).DisposeWith(disposable);
        ProfileViewModel = new ProfileViewModel(projectSeed, uiServices).DisposeWith(disposable);

        StagesViewModel.LastStageDate
            .Select(date => date?.AddDays(60))
            .Subscribe(date => FundingStructureViewModel.ExpiryDate = date)
            .DisposeWith(disposable);

        this.ValidationRule(FundingStructureViewModel.IsValid, b => b, _ => "Funding structures not valid").DisposeWith(disposable);
        this.ValidationRule(ProfileViewModel.IsValid, b => b, _ => "Profile not valid").DisposeWith(disposable);

        // Stages validation only applies to Invest projects
        var stagesValidationObservable = FundingStructureViewModel.WhenAnyValue(x => x.ProjectType)
         .CombineLatest(StagesViewModel.IsValid, (projectType, stagesValid) =>
         {
             // For Invest projects, stages must be valid
             // For Fund/Subscribe projects, stages validation is skipped
             return projectType != ProjectType.Invest || stagesValid;
         });

        this.ValidationRule(stagesValidationObservable, isValid => isValid, _ => "Stages are not valid for Invest projects").DisposeWith(disposable);

        Create = ReactiveCommand.CreateFromTask(ShowTransactionPreviewAndCreate, IsValid).Enhance();
    }

    private async Task<Result<string>> ShowTransactionPreviewAndCreate()
    {
        var dto = this.ToDto();
        string? transactionId = null;
        string? projectInfoEventId = null;

        // Step 1: Create Nostr Profile
        uiServices.Dialog.ShowMessage($"create project profile", $"create project profile");
        logger.LogInformation("[CreateProfile] Step 1: creating profile for project {ProjectName}", dto.ProjectName);
        var profileResult = await projectAppService.CreateProjectProfile(wallet.Id, projectSeed, dto);

        if (profileResult.IsFailure)
        {
            logger.LogError("[CreateProject] Failed to create Nostr profile: {Error}", profileResult.Error);
            uiServices.NotificationService.Show($"Failed to create project profile: {profileResult.Error}", "Profile Creation Failed");
            return Result.Failure<string>(profileResult.Error);
        }

        logger.LogInformation("[CreateProject] Nostr profile created successfully. Event ID: {EventId}", profileResult.Value);

        // Step 2: Create Project Info on Nostr
        uiServices.Dialog.ShowMessage($"create project info", $"create project info");
        logger.LogInformation("[CreateProject] Step 2: Creating project info on Nostr for project {ProjectName}", dto.ProjectName);
        var projectInfoResult = await projectAppService.CreateProjectInfo(wallet.Id, dto, projectSeed);

        if (projectInfoResult.IsFailure)
        {
            logger.LogError("[CreateProject] Failed to create project info: {Error}", projectInfoResult.Error);
            uiServices.NotificationService.Show($"Failed to create project info: {projectInfoResult.Error}", "Project Info Creation Failed");
            return Result.Failure<string>(projectInfoResult.Error);
        }

        projectInfoEventId = projectInfoResult.Value.EventId;
        logger.LogInformation("[CreateProject] Project info created successfully. Event ID: {EventId}", projectInfoEventId);

        // Step 3: Show Transaction Preview and Create Transaction
        logger.LogInformation("[CreateProject] Step 3: Creating blockchain transaction for project {ProjectName}", dto.ProjectName);

        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
           async feerate =>
           {
               var result = await projectAppService.CreateProject(wallet.Id, feerate, dto, projectInfoEventId, projectSeed);
               return result.Map(draft =>
                       {
                           transactionId = draft.TransactionId;
                           ITransactionDraftViewModel viewModel = new TransactionDraftViewModel(draft, uiServices);
                           return viewModel;
                       });
           },
          model =>
          {
              // Use FounderAppService to publish the transaction
              return founderAppService.SubmitTransactionFromDraft(wallet.Id, model.Model)
               .Tap(txId =>
            {
                transactionId = txId;
                uiServices.NotificationService.Show("Project created successfully!", "Success");
                logger.LogInformation("[CreateProject] Project created successfully: {TransactionId}", txId);
            })
          .Map(_ => Guid.Empty); // Convert string result to Guid for the previewer
          },
          uiServices);

        var dialogRes = await uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Review Project Creation", s => s.CommitDraft.Enhance("Create Project"));

        return dialogRes.HasValue
            ? Result.Success(transactionId ?? "Unknown")
            : Result.Failure<string>("Project creation was canceled");
    }

    public IEnhancedCommand<Result<string>> Create { get; }

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
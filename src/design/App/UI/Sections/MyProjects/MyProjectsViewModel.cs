using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Wallet.Application;
using App.UI.Shared;
using ReactiveUI;

namespace App.UI.Sections.MyProjects;

/// <summary>
/// A deployed project shown in the My Projects list after wizard completion.
/// </summary>
public class MyProjectItemViewModel
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ProjectType { get; set; } = "investment";
    public string TargetAmount { get; set; } = "0.00000000";
    public string Status { get; set; } = "Open";
    public int InvestorCount { get; set; }
    public string Raised { get; set; } = "0.00000000";
    public double Progress { get; set; }
    public string? BannerUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string StartDate { get; set; } = "";
    /// <summary>SDK project identifier for operations</summary>
    public string ProjectIdentifier { get; set; } = "";
    /// <summary>SDK wallet ID that owns this project</summary>
    public string OwnerWalletId { get; set; } = "";

    private Shared.ProjectType TypeEnum => ProjectTypeExtensions.FromLowerString(ProjectType);

    public string TypePillText => ProjectTypeTerminology.TypePillText(TypeEnum);

    // Vue uses "Invest" for the pill matching; ensure we return the expected value
    public string TypePillValue => TypeEnum.ToDisplayString();

    public string InvestorLabel => ProjectTypeTerminology.InvestorNounPlural(TypeEnum);

    public string TargetLabel => ProjectTypeTerminology.TargetLabel(TypeEnum);
}

/// <summary>
/// My Projects ViewModel — connected to SDK for founder project discovery and management.
/// Uses IProjectAppService.GetFounderProjects() to load projects owned by the user.
/// </summary>
public partial class MyProjectsViewModel : ReactiveObject
{
    private readonly IProjectAppService _projectAppService;
    private readonly IWalletAppService _walletAppService;
    private readonly Func<MyProjectItemViewModel, ManageProjectViewModel> _manageFactory;
    private readonly ICurrencyService _currencyService;

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    [Reactive] private bool showCreateWizard;
    [Reactive] private ManageProjectViewModel? selectedManageProject;
    [Reactive] private bool isLoading;

    /// <summary>The create project wizard VM, injected via DI.</summary>
    public CreateProjectViewModel CreateProjectVm { get; }

    public bool HasProjects => Projects.Count > 0;

    public string TotalRaised
    {
        get
        {
            var total = Projects.Sum(p =>
            {
                if (double.TryParse(p.Raised, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    return v;
                return 0;
            });
            return total.ToString("F5", CultureInfo.InvariantCulture);
        }
    }

    public ObservableCollection<MyProjectItemViewModel> Projects { get; } = new();

    public MyProjectsViewModel(
        IProjectAppService projectAppService,
        IWalletAppService walletAppService,
        Func<MyProjectItemViewModel, ManageProjectViewModel> manageFactory,
        CreateProjectViewModel createProjectVm,
        ICurrencyService currencyService)
    {
        _projectAppService = projectAppService;
        _walletAppService = walletAppService;
        _manageFactory = manageFactory;
        CreateProjectVm = createProjectVm;
        _currencyService = currencyService;

        // Load founder projects from SDK
        _ = LoadFounderProjectsAsync();
    }

    /// <summary>
    /// Load founder's own projects from SDK for all wallets.
    /// </summary>
    public async Task LoadFounderProjectsAsync()
    {
        IsLoading = true;

        try
        {
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsFailure) return;

            Projects.Clear();

            foreach (var meta in metadatasResult.Value)
            {
                var projectsResult = await _projectAppService.GetFounderProjects(meta.Id);
                if (projectsResult.IsFailure) continue;

                foreach (var dto in projectsResult.Value.Projects)
                {
                    var targetBtc = dto.TargetAmount / 100_000_000.0;
                    var projectType = dto.ProjectType switch
                    {
                        Angor.Shared.Models.ProjectType.Fund => "fund",
                        Angor.Shared.Models.ProjectType.Subscribe => "subscription",
                        _ => "investment"
                    };

                    Projects.Add(new MyProjectItemViewModel
                    {
                        Name = dto.Name ?? "Untitled Project",
                        Description = dto.ShortDescription ?? "",
                        ProjectType = projectType,
                        TargetAmount = targetBtc.ToString("F5", CultureInfo.InvariantCulture),
                        Status = DateTime.UtcNow < dto.FundingStartDate ? "Upcoming"
                            : DateTime.UtcNow < dto.FundingEndDate ? "Open"
                            : "Closed",
                        StartDate = dto.FundingStartDate.ToString("yyyy-MM-dd"),
                        BannerUrl = dto.Banner?.ToString(),
                        LogoUrl = dto.Avatar?.ToString(),
                        ProjectIdentifier = dto.Id?.Value ?? "",
                        OwnerWalletId = meta.Id.Value
                    });
                }
            }

            this.RaisePropertyChanged(nameof(HasProjects));
            this.RaisePropertyChanged(nameof(TotalRaised));
        }
        catch
        {
            // SDK call failed
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void LaunchCreateWizard() => ShowCreateWizard = true;

    /// <summary>
    /// Called after a project is successfully deployed.
    /// Adds the project to the list and exits the wizard.
    /// </summary>
    public void OnProjectDeployed(CreateProjectViewModel wizardVm)
    {
        Projects.Add(new MyProjectItemViewModel
        {
            Name = wizardVm.ProjectName,
            Description = wizardVm.ProjectAbout,
            ProjectType = wizardVm.ProjectType,
            TargetAmount = !string.IsNullOrEmpty(wizardVm.TargetAmount) ? wizardVm.TargetAmount : "0",
            Status = "Open",
            StartDate = wizardVm.StartDate,
            BannerUrl = wizardVm.BannerUrl,
            LogoUrl = wizardVm.ProfileUrl,
        });
        this.RaisePropertyChanged(nameof(HasProjects));
        this.RaisePropertyChanged(nameof(TotalRaised));
    }

    public void CloseCreateWizard() => ShowCreateWizard = false;

    public void ClearProjects()
    {
        Projects.Clear();
        this.RaisePropertyChanged(nameof(HasProjects));
        this.RaisePropertyChanged(nameof(TotalRaised));
    }

    public void OpenManageProject(MyProjectItemViewModel project)
    {
        SelectedManageProject = _manageFactory(project);
    }

    public void CloseManageProject() => SelectedManageProject = null;
}

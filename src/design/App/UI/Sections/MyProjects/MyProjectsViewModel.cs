using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using App.UI.Sections.MyProjects.EditProfile;
using App.UI.Shared;
using App.UI.Shared.Services;
using Microsoft.Extensions.Logging;
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
    private readonly IWalletContext _walletContext;
    private readonly Func<MyProjectItemViewModel, ManageProjectViewModel> _manageFactory;
    private readonly Func<MyProjectItemViewModel, EditProfileViewModel> _editProfileFactory;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<MyProjectsViewModel> _logger;

    /// <summary>Raised when the VM wants to show a transient toast notification.</summary>
    public event Action<string>? ToastRequested;

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    [Reactive] private bool showCreateWizard;
    [Reactive] private ManageProjectViewModel? selectedManageProject;
    [Reactive] private EditProfileViewModel? selectedEditProject;
    [Reactive] private bool isLoading;
    [Reactive] private bool isInitialLoad = true;
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
        IWalletContext walletContext,
        Func<MyProjectItemViewModel, ManageProjectViewModel> manageFactory,
        Func<MyProjectItemViewModel, EditProfileViewModel> editProfileFactory,
        CreateProjectViewModel createProjectVm,
        ICurrencyService currencyService,
        ILogger<MyProjectsViewModel> logger)
    {
        _projectAppService = projectAppService;
        _walletContext = walletContext;
        _manageFactory = manageFactory;
        _editProfileFactory = editProfileFactory;
        CreateProjectVm = createProjectVm;
        _currencyService = currencyService;
        _logger = logger;
    }

    /// <summary>
    /// Load founder's own projects from SDK for all wallets.
    /// </summary>
    public async Task LoadFounderProjectsAsync()
    {
        // Guard against concurrent loads (e.g. constructor fire-and-forget + WalletsUpdated)
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            var wallets = _walletContext.Wallets.ToList();
            var loadedProjects = await Task.Run(async () =>
            {
                var projects = new List<MyProjectItemViewModel>();

                foreach (var wallet in wallets)
                {
                    var projectsResult = await _projectAppService.GetFounderProjects(wallet.Id);
                    if (projectsResult.IsFailure) continue;

                    foreach (var dto in projectsResult.Value.Projects)
                    {
                        var targetBtc = (double)dto.TargetAmount.ToUnitBtc();
                        var projectType = dto.ProjectType switch
                        {
                            Angor.Shared.Models.ProjectType.Fund => "fund",
                            Angor.Shared.Models.ProjectType.Subscribe => "subscription",
                            _ => "investment"
                        };

                        projects.Add(new MyProjectItemViewModel
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
                            OwnerWalletId = wallet.Id.Value
                        });
                    }
                }

                return projects;
            });

            Projects.Clear();
            foreach (var project in loadedProjects)
                Projects.Add(project);

            this.RaisePropertyChanged(nameof(HasProjects));
            this.RaisePropertyChanged(nameof(TotalRaised));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadFounderProjectsAsync failed");
        }
        finally
        {
            IsLoading = false;
            IsInitialLoad = false;
        }
    }

    public void LaunchCreateWizard() => ShowCreateWizard = true;

    /// <summary>
    /// Scan the network (indexer/Nostr) for founder projects not yet in local DB.
    /// Discovers projects created from a restored wallet or if the local DB was cleared.
    /// After scanning, reloads the project list.
    /// </summary>
    public async Task ScanForProjectsAsync()
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            var wallets = _walletContext.Wallets.ToList();
            await Task.Run(async () =>
            {
                foreach (var wallet in wallets)
                {
                    // ScanFounderProjects checks all 15 derived key slots,
                    // discovers new projects, and persists them locally.
                    var result = await _projectAppService.ScanFounderProjects(wallet.Id);
                    if (result.IsFailure)
                    {
                        _logger.LogError("ScanFounderProjects failed for wallet {WalletId}: {Error}",
                            wallet.Id.Value, result.Error);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScanForProjectsAsync threw an unexpected exception");
            ToastRequested?.Invoke("Failed to scan for projects. Please try again.");
        }
        finally
        {
            IsLoading = false;
            IsInitialLoad = false;
        }

        // Reload from local DB (now includes any newly discovered projects)
        await LoadFounderProjectsAsync();
    }

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

    public void OpenEditProfile(MyProjectItemViewModel project)
    {
        SelectedEditProject = _editProfileFactory(project);
    }

    public void CloseEditProfile() => SelectedEditProject = null;
}

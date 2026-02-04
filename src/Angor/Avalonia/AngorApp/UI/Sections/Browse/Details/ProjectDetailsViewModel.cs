using System.Collections.ObjectModel;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using AngorApp.Core.Factories;
using AngorApp.UI.Flows.InvestV2;
using AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly IFullProject project;
    private readonly IProjectAppService projectAppService;
    private readonly INetworkStorage networkStorage;
    private bool enableProductionValidations;
    private ObservableCollection<INostrRelay> relays = new();

    public ProjectDetailsViewModel(
        FullProject project,
        Func<ProjectId, IFoundedProjectOptionsViewModel> foundedProjectOptionsFactory,
        UIServices uiServices, 
        INavigator navigator,
        IProjectAppService projectAppService,
        INetworkStorage networkStorage)
    {
        this.project = project;
        this.projectAppService = projectAppService;
        this.networkStorage = networkStorage;

        enableProductionValidations = uiServices.EnableProductionValidations();

        if (enableProductionValidations)
        {
            // todo: when fund and subscribe are implemented there is no limit to investment period
            IsInsideInvestmentPeriod = DateTime.Now <= project.FundingEndDate;
        }
        else
        {
            IsInsideInvestmentPeriod = true;
        }
        
        Invest = EnhancedCommand.CreateWithResult(() => navigator.Go<IInvestViewModel>() , Observable.Return(IsInsideInvestmentPeriod));
        Invest.HandleErrorsWith(uiServices.NotificationService, "Investment failed");
        
        FoundedProjectOptions = foundedProjectOptionsFactory(project.ProjectId);
        
        // Initialize with default relays from settings
        InitializeRelaysFromSettings();
        
        // Fetch relays from the user's Nostr account (NIP-65)
        FetchRelaysFromNostrAccount(project.NostrNpubKeyHex);
    }

    private void InitializeRelaysFromSettings()
    {
        var settings = networkStorage.GetSettings();
        var settingsRelays = settings.Relays
            .Where(r => System.Uri.TryCreate(r.Url, UriKind.Absolute, out _))
            .Select(r => new NostrRelay { Uri = new System.Uri(r.Url) })
            .Cast<INostrRelay>()
            .ToList();
        
        relays = new ObservableCollection<INostrRelay>(settingsRelays);
    }

    private async void FetchRelaysFromNostrAccount(string nostrPubKey)
    {
        if (string.IsNullOrEmpty(nostrPubKey))
            return;

        var result = await projectAppService.GetRelaysForNpubAsync(nostrPubKey);
        
        if (result.IsSuccess && result.Value.RelayUrls.Any())
        {
            var userRelays = result.Value.RelayUrls
                .Where(url => System.Uri.TryCreate(url, UriKind.Absolute, out _))
                .Select(url => new NostrRelay { Uri = new System.Uri(url) })
                .Cast<INostrRelay>()
                .ToList();

            if (userRelays.Any())
            {
                // Update relays on the UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    relays.Clear();
                    foreach (var relay in userRelays)
                    {
                        relays.Add(relay);
                    }
                });
            }
        }
    }

    public bool IsInsideInvestmentPeriod { get; }
    public TimeSpan? NextRelease { get; }
    public IStage? CurrentStage { get; }
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; }

    public IEnhancedCommand<Result<Unit>> Invest { get; }

    public IEnumerable<INostrRelay> Relays => relays;

    public IFullProject Project => project;
}

public class NostrRelay : INostrRelay
{
    public Uri Uri { get; init; } = null!;
}
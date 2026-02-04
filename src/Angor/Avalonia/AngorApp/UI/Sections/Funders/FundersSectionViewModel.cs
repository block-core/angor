using System.Reactive.Disposables;
using System.Linq;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Funders;

[Section("Funders", icon: "fa-user-group", sortIndex: 5)]
[SectionGroup("FOUNDER")]
public partial class FundersSectionViewModel : ReactiveObject, IFundersSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly IFounderAppService founderAppService;
    private readonly IProjectAppService projectAppService;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;

    [Reactive] private int selectedTabIndex;
    [Reactive] private IEnumerable<IFunderApprovalItemViewModel> pending;
    [Reactive] private IEnumerable<IFunderApprovalItemViewModel> approved;
    private readonly ObservableAsPropertyHelper<int> pendingCount;
    private readonly ObservableAsPropertyHelper<int> approvedCount;

    public FundersSectionViewModel(IFounderAppService founderAppService, IProjectAppService projectAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.founderAppService = founderAppService;
        this.projectAppService = projectAppService;
        this.uiServices = uiServices;
        this.walletContext = walletContext;

        Refresh = ReactiveCommand.CreateFromTask(Load).Enhance().DisposeWith(disposable);

        pendingCount = this.WhenAnyValue(x => x.Pending)
            .Select(items => items?.Count() ?? 0)
            .ToProperty(this, x => x.PendingCount)
            .DisposeWith(disposable);

        approvedCount = this.WhenAnyValue(x => x.Approved)
            .Select(items => items?.Count() ?? 0)
            .ToProperty(this, x => x.ApprovedCount)
            .DisposeWith(disposable);

        var canApproveAll = this.WhenAnyValue(x => x.PendingCount).Select(count => count > 0);
        ApproveAll = ReactiveCommand.CreateFromTask(ApproveAllPending, canApproveAll)
            .Enhance()
            .DisposeWith(disposable);
    }

    public int PendingCount => pendingCount.Value;
    public int ApprovedCount => approvedCount.Value;

    public bool HasPending => PendingCount > 0;

    public bool ShowPendingEmptyState => !HasPending;

    public IEnhancedCommand Refresh { get; }

    public IEnhancedCommand ApproveAll { get; }

    private async Task Load()
    {
        var result = await walletContext.RequiresWallet(async wallet =>
        {
            var projectsResult = await projectAppService.GetFounderProjects(wallet.Id);
            if (projectsResult.IsFailure)
            {
                return CSharpFunctionalExtensions.Result.Failure<(IWallet wallet, IEnumerable<(ProjectId projectId, string projectName, Investment investment)> items)>(projectsResult.Error);
            }

            var projects = projectsResult.Value.Projects.ToList();

            var all = new List<(ProjectId projectId, string projectName, Investment investment)>();

            foreach (var project in projects)
            {
                var investmentsResult = await founderAppService.GetProjectInvestments(new GetProjectInvestments.GetProjectInvestmentsRequest(wallet.Id, project.Id));
                if (investmentsResult.IsFailure)
                {
                    continue;
                }

                foreach (var investment in investmentsResult.Value.Investments)
                {
                    all.Add((project.Id, project.Name, investment));
                }
            }

            return CSharpFunctionalExtensions.Result.Success((wallet, items: all.AsEnumerable()));
        });

        if (result.IsFailure)
        {
            await uiServices.NotificationService.Show($"Could not load funders: {result.Error}", "Error");
            return;
        }

        var walletValue = result.Value.wallet;

        var viewModels = result.Value.items
            .Select(tuple => (IFunderApprovalItemViewModel)new FunderApprovalItemViewModel(
                tuple.projectId,
                tuple.projectName,
                tuple.investment,
                walletValue,
                founderAppService,
                uiServices,
                Load))
            .OrderByDescending(vm => vm.Timestamp)
            .ToList();

        pending = viewModels.Where(vm => !vm.IsApproved).ToList();
        approved = viewModels.Where(vm => vm.IsApproved).ToList();
    }

    private async Task ApproveAllPending()
    {
        var pendingSnapshot = Pending.ToList();
        if (pendingSnapshot.Count == 0)
        {
            return;
        }

        var confirmationResult = await uiServices.Dialog.ShowConfirmation(
            "Approve all",
            $"Do you want to approve {pendingSnapshot.Count} investment(s)?");

        if (confirmationResult.HasNoValue || !confirmationResult.Value)
        {
            return;
        }

        var successes = 0;
        var failures = 0;

        foreach (var item in pendingSnapshot)
        {
            try
            {
                var ok = await item.ApproveAsync();
                if (ok)
                {
                    successes++;
                }
                else
                {
                    failures++;
                }
            }
            catch
            {
                failures++;
            }
        }

        await Load();

        if (failures == 0)
        {
            await uiServices.NotificationService.Show($"Approved {successes} investment(s)", "Success");
            return;
        }

        await uiServices.NotificationService.Show($"Approved {successes} investment(s), {failures} failed/cancelled", "Error");
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}

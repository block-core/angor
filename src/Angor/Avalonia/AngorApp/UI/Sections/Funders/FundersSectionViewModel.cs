using DynamicData;
using Zafiro.UI.Shell.Utils;
using AngorApp.UI.Sections.Funders.Items;
using AngorApp.UI.Sections.Funders.Grouping;

namespace AngorApp.UI.Sections.Funders;

[Section("Funders", "fa-user-group", 5)]
[SectionGroup("FOUNDER")]
public class FundersSectionViewModel : IFundersSectionViewModel
{
    private readonly UIServices uiServices;

    public FundersSectionViewModel(UIServices uiServices)
    {
        this.uiServices = uiServices;

        RefreshableCollection<IFunderItem, string> funders = RefreshableCollection.Create(
            GetItems,
            item => item.InvestorNpub);

        Groups =
        [
            new FunderGroup("Pending", funders.Changes.Filter(item => item.Status == FunderStatus.Pending)),
            new FunderGroup("Approved", funders.Changes.Filter(item => item.Status == FunderStatus.Approved)),
            new FunderGroup("Rejected", funders.Changes.Filter(item => item.Status == FunderStatus.Rejected))
        ];

        IsEmpty = funders.Changes.IsEmpty();
        Load = funders.Refresh;
    }

    public IEnumerable<IFunderGroup> Groups { get; }
    public IEnhancedCommand Load { get; }
    public IObservable<bool> IsEmpty { get; }

    private async Task<Result<IEnumerable<IFunderItem>>> GetItems()
    {
        return Result.Success<IEnumerable<IFunderItem>>(
        [
            new FunderItem(uiServices: uiServices)
            {
                Amount = new AmountUI(10000),
                InvestorNpub = "investor_npub1",
                Status = FunderStatus.Pending
            },
            new FunderItem(uiServices: uiServices)
            {
                Amount = new AmountUI(20000),
                InvestorNpub = "investor_npub2",
                Status = FunderStatus.Approved
            },
            new FunderItem(uiServices: uiServices)
            {
                Amount = new AmountUI(5550000),
                InvestorNpub = "investor_npub3",
                Status = FunderStatus.Rejected
            },
            new FunderItem(uiServices: uiServices)
            {
                Amount = new AmountUI(95400),
                InvestorNpub = "investor_npub4",
                Status = FunderStatus.Pending
            },
        ]);
    }
}
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;

public partial class InvestmentViewModel : ReactiveObject, IInvestmentViewModel, IDisposable
{
    [ReactiveUI.SourceGenerators.Reactive]
    private bool areDetailsShown;

    private readonly CompositeDisposable disposable = new();

    public InvestmentViewModel(IGrouping<InvestmentGroupKey, Investment> group, Func<Task<bool>> onApprove)
    {
        var sorted = group.OrderByDescending(x => x.CreatedOn)
            .Select(investment => new InvestmentChildViewModel(investment))
            .ToList();

        MostRecentInvestment = sorted.First();
        OtherInvestments = sorted.Skip(1);

        Approve = ReactiveCommand.CreateFromTask(async () =>
        {
            var approved = await onApprove();
            if (approved)
            {
                MostRecentInvestment.Status = InvestmentStatus.FounderSignaturesReceived;
            }
            
        }, this.WhenAnyValue(model => model.MostRecentInvestment.Status, x => x == InvestmentStatus.PendingFounderSignatures)).Enhance().DisposeWith(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public IEnumerable<IInvestmentChild> OtherInvestments { get; }

    public IInvestmentChild MostRecentInvestment { get; }

    public IEnhancedCommand Approve { get; }
}
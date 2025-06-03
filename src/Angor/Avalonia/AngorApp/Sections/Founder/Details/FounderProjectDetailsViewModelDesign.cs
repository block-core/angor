using Angor.Contexts.Funding.Founder;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public class FounderProjectDetailsViewModelDesign : IFounderProjectDetailsViewModel
{
    public string Name { get; } = "Test";

    public IEnumerable<IdentityContainer<IInvestmentViewModel>> Investments { get; } = new List<IdentityContainer<IInvestmentViewModel>>()
    {
        new()
        {
            Content = new InvestmentViewModelDesign(new AmountUI(12122), "nostr pub key", DateTimeOffset.Now, InvestmentStatus.Approved)
            {
                Status = InvestmentStatus.Pending,
            }
        },
        new()
        {
            Content = new InvestmentViewModelDesign(new AmountUI(1234233), "nostr pub key", DateTimeOffset.Now.AddHours(-2),  InvestmentStatus.Invested) 
            {
                Status = InvestmentStatus.Invested,
            }
        },
        new()
        {
            Content = new InvestmentViewModelDesign(new AmountUI(423445), "nostr pub key", DateTimeOffset.Now.AddHours(-4), InvestmentStatus.Pending)
            {
                Status = InvestmentStatus.Approved,
            }
        },
    };

    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
    public Uri? BannerUrl { get; set; }
    public string ShortDescription { get; } = "Short description, Bitcoin ONLY.";
}

public record InvestmentViewModelDesign(IAmountUI Amount, string InvestorNostrPubKey, DateTimeOffset CreatedOn, InvestmentStatus Status) : IInvestmentViewModel
{
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; } = ReactiveCommand.Create(() => Maybe.From(Result.Success(true)), Observable.Return(Status == InvestmentStatus.Pending)).Enhance();
}
using Angor.Contexts.Funding.Founder.Operations;
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
            Content = new InvestmentViewModelDesign(new AmountUI(12122), "nostr pub key", DateTimeOffset.Now, true)
            {
                Status = InvestmentStatus.Pending,
            }
        },
        new()
        {
            Content = new InvestmentViewModelDesign(new AmountUI(1234233), "nostr pub key", DateTimeOffset.Now.AddHours(-2), false) 
            {
                Status = InvestmentStatus.Invested,
            }
        },
        new()
        {
            Content = new InvestmentViewModelDesign(new AmountUI(423445), "nostr pub key", DateTimeOffset.Now.AddHours(-4), true)
            {
                Status = InvestmentStatus.Approved,
            }
        },
    };

    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
    public Uri? BannerUrl { get; set; }
    public string ShortDescription { get; } = "Short description, Bitcoin ONLY.";
}

public record InvestmentViewModelDesign(IAmountUI Amount, string InvestorNostrPubKey, DateTimeOffset Created, bool IsApproved) : IInvestmentViewModel
{
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }
    public InvestmentStatus Status { get; set; } = InvestmentStatus.Pending;
}

public interface IInvestmentViewModel
{
    public IAmountUI Amount { get; }
    public string InvestorNostrPubKey { get; }
    public DateTimeOffset Created { get; }
    public bool IsApproved { get; }
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }
    public InvestmentStatus Status { get; }
}

public enum InvestmentStatus
{
    None,
    Pending,
    Approved,
    Invested,
}
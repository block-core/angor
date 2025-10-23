using Angor.Contexts.Funding.Founder.Domain;
using Angor.Contexts.Funding.Founder.Operations;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;

public class InvestmentGroupKey
{
    public Investment Investment { get; }
    public IWallet Wallet { get; }

    public InvestmentGroupKey(Investment investment, IWallet wallet)
    {
        Investment = investment;
        Wallet = wallet;
    }

    protected bool Equals(InvestmentGroupKey other)
    {
        return Investment.InvestorNostrPubKey.Equals(other.Investment.InvestorNostrPubKey);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((InvestmentGroupKey)obj);
    }

    public override int GetHashCode()
    {
        return Investment.InvestorNostrPubKey.GetHashCode();
    }
}
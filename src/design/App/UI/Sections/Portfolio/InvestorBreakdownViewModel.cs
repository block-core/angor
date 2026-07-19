using System.Collections.ObjectModel;
using System.Globalization;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Operations;

namespace App.UI.Sections.Portfolio;

/// <summary>
/// ViewModel for a single row in the investor breakdown table.
/// </summary>
public class InvestorShareRowViewModel
{
    public int Rank { get; init; }
    public string InvestorPublicKey { get; init; } = "";
    public string ShortKey { get; init; } = "";
    public string TotalInvested { get; init; } = "0.00000000";
    public string SharePercentage { get; init; } = "0.00%";
    public string AmountClaimed { get; init; } = "0.00000000";
    public string ClaimedPercentage { get; init; } = "0.00%";
    public bool IsCurrentUser { get; init; }
    public string CurrencySymbol { get; init; } = "BTC";
}

/// <summary>
/// ViewModel for the investor breakdown modal.
/// Shows all investors in a project with their share percentages.
/// </summary>
public class InvestorBreakdownViewModel
{
    public string ProjectName { get; }
    public string TotalInvested { get; }
    public int TotalInvestors { get; }
    public string CurrencySymbol { get; }
    public string ProjectType { get; }
    public bool IsFundType { get; }

    /// <summary>
    /// Context note for Fund projects: "Shares are calculated as of now.
    /// New funds can always be added, which will change the percentages."
    /// </summary>
    public string? ShareContextNote { get; }

    public ObservableCollection<InvestorShareRowViewModel> Investors { get; } = new();

    public InvestorBreakdownViewModel(
        GetInvestorShares.GetInvestorSharesResponse data,
        string projectName,
        string projectType,
        string currencySymbol)
    {
        ProjectName = projectName;
        ProjectType = projectType;
        CurrencySymbol = currencySymbol;
        IsFundType = projectType == "fund";
        TotalInvested = ((double)new Amount(data.TotalInvested).Sats.ToUnitBtc())
            .ToString("F8", CultureInfo.InvariantCulture);
        TotalInvestors = data.TotalInvestors;

        ShareContextNote = IsFundType
            ? "Shares are calculated as of now. New funds can always be added, which will change the percentages."
            : null;

        int rank = 1;
        foreach (var investor in data.Investors)
        {
            var key = investor.InvestorPublicKey;
            var shortKey = key.Length > 12
                ? $"{key[..6]}...{key[^6..]}"
                : key;

            Investors.Add(new InvestorShareRowViewModel
            {
                Rank = rank++,
                InvestorPublicKey = key,
                ShortKey = shortKey,
                TotalInvested = ((double)new Amount(investor.TotalInvested).Sats.ToUnitBtc())
                    .ToString("F8", CultureInfo.InvariantCulture),
                SharePercentage = $"{investor.SharePercentage:F2}%",
                AmountClaimed = ((double)new Amount(investor.AmountClaimedByFounder).Sats.ToUnitBtc())
                    .ToString("F8", CultureInfo.InvariantCulture),
                ClaimedPercentage = $"{investor.ClaimedPercentage:F2}%",
                CurrencySymbol = currencySymbol
            });
        }
    }
}

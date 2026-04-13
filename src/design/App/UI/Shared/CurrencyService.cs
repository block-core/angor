using System.Globalization;
using Angor.Shared;

namespace App.UI.Shared;

/// <summary>
/// Singleton service that reads the currency symbol from <see cref="INetworkConfiguration"/>.
/// Every property re-reads from the network config so it stays correct after a network switch.
/// </summary>
public class CurrencyService : ICurrencyService
{
    private readonly INetworkConfiguration networkConfiguration;

    public CurrencyService(INetworkConfiguration networkConfiguration)
    {
        this.networkConfiguration = networkConfiguration;
    }

    public string Symbol => networkConfiguration.GetNetwork().CoinTicker;

    public string FormatBtc(double btcValue, string format = "F8")
        => $"{btcValue.ToString(format, CultureInfo.InvariantCulture)} {Symbol}";

    public string AmountLabel => $"Amount ({Symbol})";
    public string TargetAmountLabel => $"Target Amount ({Symbol}) *";
    public string GoalLabel => $"Goal ({Symbol}) *";
    public string MinInvestmentHint => $"Minimum investment: {Constants.MinInvestmentAmount} {Symbol}";
    public string MinerFeeDisplay => $"~{Constants.MinerFee:F8} {Symbol}";
    public string PricePerPeriodLabel => $"Price per period ({Symbol}) *";
}

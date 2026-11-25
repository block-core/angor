using Angor.Shared.Integration.Lightning.Models;
using Xunit;

namespace Angor.Test;

public class BoltzSwapFeesTests
{
    [Theory]
    [InlineData(11322)]
    [InlineData(25000)]
    [InlineData(100000)]
    public void CalculateInvoiceAmount_DeliversAtLeastTheNeededOnChainAmount(long needed)
    {
        // Live fee table: 0.6% + 154 sats lockup miner fee
        var fees = new BoltzSwapFees { Percentage = 0.6m, MinerFees = 154, ClaimMinerFee = 111 };

        var invoice = fees.CalculateInvoiceAmount(needed);

        Assert.True(invoice * 0.994m - 154 >= needed, $"invoice {invoice} delivers too little for {needed}");
        Assert.True(fees.CalculateOnChainAmount(invoice) >= needed);
    }
}
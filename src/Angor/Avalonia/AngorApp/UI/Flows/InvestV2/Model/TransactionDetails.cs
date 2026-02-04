namespace AngorApp.UI.Flows.InvestV2.Model
{
    public record TransactionDetails(IAmountUI AmountToInvest, IAmountUI MinerFee, IAmountUI AngorFee)
    {
        public IAmountUI Total => new AmountUI(AmountToInvest.Sats + MinerFee.Sats + AngorFee.Sats);

        public static TransactionDetails Empty()
        {
            return new TransactionDetails(AmountUI.FromBtc(0), AmountUI.FromBtc(0), AmountUI.FromBtc(0));
        }
    }
}
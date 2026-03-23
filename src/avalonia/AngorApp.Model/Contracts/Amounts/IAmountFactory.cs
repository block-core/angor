namespace AngorApp.Model.Contracts.Amounts;

public interface IAmountFactory
{
    IAmountUI Create(long sats);
    string CurrencySymbol { get; }
}

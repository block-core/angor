namespace Angor.UI.Model;

public interface IAmountFactory
{
    IAmountUI Create(long sats);
    string CurrencySymbol { get; }
}

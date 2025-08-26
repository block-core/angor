namespace Angor.UI.Model;

public interface IStage
{
    DateTime ReleaseDate { get; }
    long Amount { get; }
    IAmountUI AmountUI => new AmountUI(Amount);
    int Index { get; }
    double RatioOfTotal { get; }
}
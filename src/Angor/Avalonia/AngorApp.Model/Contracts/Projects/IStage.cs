namespace AngorApp.Model.Contracts.Projects;

public interface IStage
{
    DateTime ReleaseDate { get; }
    long Amount { get; }
    IAmountUI AmountUI { get; }
    int Index { get; }
    decimal RatioOfTotal { get; }
}

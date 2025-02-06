namespace Angor.UI.Model;

public interface IStage
{
    DateOnly ReleaseDate { get; }
    long Amount { get; }
    int Index { get; }
    double Weight { get; }
}
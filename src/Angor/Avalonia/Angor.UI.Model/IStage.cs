namespace Angor.UI.Model;

public interface IStage
{
    DateTime ReleaseDate { get; }
    long Amount { get; }
    int Index { get; }
    double Weight { get; }
}
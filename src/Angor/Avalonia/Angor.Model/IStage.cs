namespace AngorApp.Model;

public interface IStage
{
    DateOnly ReleaseDate { get; }
    uint Amount { get; }
    int Index { get; }
    double Weight { get; }
}
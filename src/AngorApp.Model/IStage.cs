namespace AngorApp.Model;

public interface IStage
{
    DateOnly ReleaseDate { get; }
    decimal Amount { get; }
    int Index { get; }
    double Weight { get; }
}
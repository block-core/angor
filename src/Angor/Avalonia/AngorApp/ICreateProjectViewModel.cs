namespace AngorApp;

public interface ICreateProjectViewModel
{
    public IEnumerable<ICreateProjectStage> Stages { get; }
    public DateTime StartDate { get; }
    public DateTime EndDate { get; set; }
}

public interface ICreateProjectStage
{
    public double Percent { get; set; }
    public DateTimeOffset? StartDate { get; set; }
}

public class CreateProjectViewModel : ReactiveObject, ICreateProjectViewModel
{
    public IEnumerable<ICreateProjectStage> Stages { get; } = [];
    public DateTime StartDate { get; }
    public DateTime EndDate { get; set; }
}
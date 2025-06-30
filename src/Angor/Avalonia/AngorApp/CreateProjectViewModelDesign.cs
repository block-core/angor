namespace AngorApp;

public class CreateProjectViewModelDesign : ICreateProjectViewModel
{
    public IEnumerable<ICreateProjectStage> Stages { get; set; } = new List<ICreateProjectStage>();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class CreateProjectStageDesign : ICreateProjectStage
{
    public double Percent { get; set; }
    public DateTimeOffset? StartDate { get; set; }
}
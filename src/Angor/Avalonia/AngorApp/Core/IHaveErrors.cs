namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public interface IHaveErrors
{
    ICollection<string> Errors { get; }
}
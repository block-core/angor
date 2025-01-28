using System.Linq;
using System.Threading.Tasks;
using Angor.UI.Model;
using AngorApp.Sections.Browse;
using CSharpFunctionalExtensions;

namespace AngorApp.Design;

public class ProjectServiceDesign : IProjectService
{
    public async Task<IList<IProject>> Latest()
    {
        var projects = SampleData.GetProjects().ToList();
        return projects;
    }

    public async Task<Maybe<IProject>> FindById(string projectId)
    {
        return SampleData.GetProjects().TryFirst();
    }
}
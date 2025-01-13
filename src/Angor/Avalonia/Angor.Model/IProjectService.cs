using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IProjectService
{
    Task<IList<IProject>> Latest();
    Task<Maybe<IProject>> FindById(string projectId);
}
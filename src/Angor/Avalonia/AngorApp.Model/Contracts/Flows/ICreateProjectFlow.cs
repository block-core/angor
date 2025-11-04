using CSharpFunctionalExtensions;

namespace AngorApp.Model.Contracts.Flows;

public interface ICreateProjectFlow
{
    Task<Result<Maybe<string>>> CreateProject();
}
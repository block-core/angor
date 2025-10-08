using CSharpFunctionalExtensions;

namespace Angor.UI.Model.Flows;

public interface ICreateProjectFlow
{
    Task<Result<Maybe<string>>> CreateProject();
}
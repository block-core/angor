using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface ICreateProjectFlow
{
    Task<Result<Maybe<string>>> CreateProject();
}
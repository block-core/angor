using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Services;

public interface IDatabaseManagementService
{
    Task<Result> DeleteAllDataAsync();
}

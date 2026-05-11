using Angor.Primitives;

namespace Angor.Sdk.Funding.Services;

public interface IDatabaseManagementService
{
    Task<Result> DeleteAllDataAsync();
}

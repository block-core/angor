using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class Extensions
{
    public static Task<Result<IEnumerable<TResult>>> Traverse<TInput, TResult>(this Task<Result<IEnumerable<TInput>>> result, Func<TInput, Task<Result<TResult>>> transform)
    {
        return result.MapEach(transform).Combine();
    }
}
using AspectInjector.Broker;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;

namespace Angor.Contests.CrossCutting;

[Aspect(Scope.PerInstance)]
[Injection(typeof(MemoizeTimedAttribute))]
public class MemoizeTimedAttribute : Attribute
{
    /// <summary>
    /// Cache expiration time in seconds
    /// </summary>
    public int ExpirationInSeconds { get; set; } = 600;

    private readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());

    [Advice(Kind.Around, Targets = Target.Method)]
    [UsedImplicitly]
    public object Handle(
        [Argument(Source.Arguments)] object[] args,
        [Argument(Source.Target)] Func<object[], object> method,
        [Argument(Source.Name)] string methodName,
        [Argument(Source.ReturnType)] Type returnType)
    {
        // Generates the cache key based on the method name and its arguments.
        var cacheKey = $"{methodName}-{string.Join("_", args)}";

        if (cache.TryGetValue(cacheKey, out var cachedResult))
        {
            // Checks if the method returns Task<T>
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Gets the T type of Task<T>
                var resultType = returnType.GetGenericArguments()[0];
                // Calls Task.FromResult<T>(cachedResult) to create a Task<T> with the cached value
                var fromResultMethod = typeof(Task)
                    .GetMethod(nameof(Task.FromResult))
                    .MakeGenericMethod(resultType);
                return fromResultMethod.Invoke(null, new object[] { cachedResult });
            }
            return cachedResult;
        }

        // Executes the original method
        var result = method(args);

        if (result is Task task)
        {
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // If it is Task<T>, waits for its result, caches it, and returns it properly
                return CacheTaskResultAsync((dynamic)result, cacheKey);
            }
            else
            {
                // For Task without a generic result, caches the entire task
                task.ContinueWith(t =>
                {
                    cache.Set(cacheKey, task, TimeSpan.FromSeconds(ExpirationInSeconds));
                });
                return result;
            }
        }
        else
        {
            // For synchronous methods, caches the result and returns it
            cache.Set(cacheKey, result, TimeSpan.FromSeconds(ExpirationInSeconds));
            return result;
        }
    }

    // Wraps the task result in the cache and returns it as Task<T>
    private async Task<T> CacheTaskResultAsync<T>(Task<T> task, string cacheKey)
    {
        T result = await task;
        cache.Set(cacheKey, result, TimeSpan.FromSeconds(ExpirationInSeconds));
        return result;
    }
}
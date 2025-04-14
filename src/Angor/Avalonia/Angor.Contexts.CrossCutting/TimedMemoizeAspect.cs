using AspectInjector.Broker;
using Microsoft.Extensions.Caching.Memory;

namespace Angor.Contests.CrossCutting;

[Aspect(Scope.PerInstance)]
[Injection(typeof(MemoizeTimedAttribute))]
public class MemoizeTimedAttribute : Attribute
{
    /// <summary>
    /// Duración de la caché en segundos. Por defecto, 600 (10 minutos).
    /// </summary>
    public int ExpirationInSeconds { get; set; } = 600;

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [Advice(Kind.Around, Targets = Target.Method)]
    public object Handle(
        [Argument(Source.Arguments)] object[] args,
        [Argument(Source.Target)] Func<object[], object> method,
        [Argument(Source.Name)] string methodName,
        [Argument(Source.ReturnType)] Type returnType)
    {
        // Genera la clave de caché a partir del nombre del método y sus argumentos.
        var cacheKey = $"{methodName}-{string.Join("_", args)}";

        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            // Verifica si el método retorna Task<T>
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Obtiene el tipo T de Task<T>
                var resultType = returnType.GetGenericArguments()[0];
                // Invoca Task.FromResult<T>(cachedResult) para crear un Task<T> con el valor cacheado
                var fromResultMethod = typeof(Task)
                    .GetMethod(nameof(Task.FromResult))
                    .MakeGenericMethod(resultType);
                return fromResultMethod.Invoke(null, new object[] { cachedResult });
            }
            return cachedResult;
        }

        // Ejecuta el método original
        var result = method(args);

        if (result is Task task)
        {
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Si es Task<T>, espera su resultado, lo cachea y lo retorna correctamente
                return CacheTaskResultAsync((dynamic)result, cacheKey);
            }
            else
            {
                // Para Task sin resultado genérico, se cachea la tarea entera
                task.ContinueWith(t =>
                {
                    _cache.Set(cacheKey, task, TimeSpan.FromSeconds(ExpirationInSeconds));
                });
                return result;
            }
        }
        else
        {
            // Para métodos sincrónicos, cachea el resultado y lo retorna
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(ExpirationInSeconds));
            return result;
        }
    }

    // Envuelve el resultado de la tarea en la caché y lo retorna como Task<T>
    private async Task<T> CacheTaskResultAsync<T>(Task<T> task, string cacheKey)
    {
        T result = await task;
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(ExpirationInSeconds));
        return result;
    }
}
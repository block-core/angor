using System;
using System.IO;
using System.Text.Json;
using Angor.Contests.CrossCutting;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class FileStore : IStore
{
    private readonly string appDataPath;

    public FileStore(IApplicationStorage storage, ProfileContext profileContext)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(profileContext);

        var directory = storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);
        appDataPath = directory;
    }

    public async Task<Result> Save<T>(string key, T data)
    {
        return from filePath in Result.Try(() => Path.Combine(appDataPath, key))
            from contents in Result.Try(() => JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }))
            select Result.Try(() => File.WriteAllTextAsync(filePath, contents))
                .Bind(Result.Success);
    }

    public Task<Result<T>> Load<T>(string key)
    {
        return Result.Try(() => Path.Combine(appDataPath, key))
            .TapTry(CreateFile)
            .MapTry(s => File.ReadAllTextAsync(s))
            .Ensure(x => !string.IsNullOrWhiteSpace(x), $"Could not read file {key}")
            .Bind(json => Result.Try(() => JsonSerializer.Deserialize<T>(json))
                .Ensure(x => x != null, $"Could not deserialize {json} as {typeof(T)}")
                .Map(x => x!)
            );
    }

    private static void CreateFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        using (File.Create(path)) { }
    }
}

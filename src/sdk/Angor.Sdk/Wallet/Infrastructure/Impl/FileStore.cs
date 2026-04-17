using System;
using System.IO;
using System.Text.Json;
using Angor.Sdk.Common;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

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
        try
        {
            var filePath = Path.Combine(appDataPath, key);
            var contents = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, contents);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
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

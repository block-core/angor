using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Primitives;

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

    public async Task<Result<T>> Load<T>(string key)
    {
        try
        {
            var path = Path.Combine(appDataPath, key);
            CreateFile(path);
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return Result.Failure<T>($"Could not read file {key}");

            var deserialized = JsonSerializer.Deserialize<T>(json);
            if (deserialized == null)
                return Result.Failure<T>($"Could not deserialize {json} as {typeof(T)}");

            return Result.Success(deserialized);
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(ex.Message);
        }
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

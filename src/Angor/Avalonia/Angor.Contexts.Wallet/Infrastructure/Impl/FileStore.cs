using System;
using System.IO;
using System.Threading.Tasks;
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

        appDataPath = storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);
    }

    public async Task<Result> Save<T>(string key, T data)
    {
        try
        {
            var filePath = Path.Combine(appDataPath, key);
            EnsureDirectory(filePath);
            var contents = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, contents).ConfigureAwait(false);
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
            var filePath = Path.Combine(appDataPath, key);
            CreateFile(filePath);
            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Result.Failure<T>($"Could not read file {key}");
            }

            var deserialized = JsonSerializer.Deserialize<T>(json);
            return deserialized is null
                ? Result.Failure<T>($"Could not deserialize {json} as {typeof(T)}")
                : Result.Success(deserialized);
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(ex.Message);
        }
    }

    private static void CreateFile(string path)
    {
        EnsureDirectory(path);

        if (File.Exists(path))
        {
            return;
        }

        using var _ = File.Create(path);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
    }
}

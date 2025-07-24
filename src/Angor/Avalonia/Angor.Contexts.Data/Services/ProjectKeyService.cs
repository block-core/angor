using Angor.Contexts.Data.Entities;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Angor.Contexts.Data.Services;

public class ProjectKeyService(AngorDbContext context) : IProjectKeyService
{
    
    public async Task<Result<IEnumerable<(string, string)>>> GetCachedProjectKeys(Guid walletId)
    {
        try
        {
            var projectKeys = await context.ProjectKeys
                .Where(pk => pk.WalletId == walletId)
                .Select(pk => new { pk.ProjectId, pk.NostrPubKey })
                .ToListAsync();

            var result = projectKeys.Select(pk => (pk.ProjectId, pk.NostrPubKey));
        
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<(string, string)>>($"Failed to retrieve cached project keys: {ex.Message}");
        }
    }

    public async Task<Result> SaveProjectKeys(Guid walletId, IEnumerable<(string, string)> projectKeys)
    {
        try
        {
            var now = DateTime.UtcNow;
        
            // First, check if keys already exist for this wallet
            var existingKeys = await context.ProjectKeys
                .Where(pk => pk.WalletId == walletId)
                .ToListAsync();

            var projectKeysList = projectKeys.ToList();
            var keysToAdd = new List<ProjectKey>();
        
            foreach (var (projectId, nostrPubKey) in projectKeysList)
            {
                var existingKey = existingKeys.FirstOrDefault(ek => ek.ProjectId == projectId);
            
                if (existingKey == null)
                {
                    // Add new key
                    keysToAdd.Add(new ProjectKey
                    {
                        Id = Guid.NewGuid(),
                        WalletId = walletId,
                        ProjectId = projectId,
                        NostrPubKey = nostrPubKey,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                else if (existingKey.NostrPubKey != nostrPubKey)
                {
                    // Update existing key if NostrPubKey has changed
                    existingKey.NostrPubKey = nostrPubKey;
                    existingKey.UpdatedAt = now;
                }
            }
        
            if (keysToAdd.Any())
            {
                await context.ProjectKeys.AddRangeAsync(keysToAdd);
            }
        
            await context.SaveChangesAsync();
        
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save project keys: {ex.Message}");
        }
    }

    public async Task<Result<bool>> HasCachedProjectKeys(Guid walletId)
    {
        try
        {
            var hasKeys = await context.ProjectKeys
                .AnyAsync(pk => pk.WalletId == walletId);
            
            return Result.Success(hasKeys);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>($"Failed to check for cached project keys: {ex.Message}");
        }
    }
}
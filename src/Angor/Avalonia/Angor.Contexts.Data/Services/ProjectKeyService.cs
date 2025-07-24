using Angor.Contexts.Data.Entities;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Angor.Contexts.Data.Services;

public class ProjectKeyService(AngorDbContext context) : IProjectKeyService
{
    
    public async Task<Result<IEnumerable<ProjectKey>>> GetCachedProjectKeys(Guid walletId)
    {
        try
        {
            var projectKeys = await context.ProjectKeys
                .Where(pk => pk.WalletId == walletId)
                .ToListAsync();

            return Result.Success(projectKeys.AsEnumerable());
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<ProjectKey>>($"Failed to retrieve cached project keys: {ex.Message}");
        }
    }

    public async Task<Result> SaveProjectKeys(Guid walletId, IEnumerable<ProjectKey> projectKeys)
    {
        try
        {
            var now = DateTime.UtcNow;
        
            // First, check if keys already exist for this wallet
            var existingKeys = await context.ProjectKeys
                .Where(pk => pk.WalletId == walletId)
                .ToListAsync();

            //var projectKeysList = projectKeys.ToList();
            var keysToAdd = projectKeys.Where(pk => existingKeys.All(ek => ek.ProjectId != pk.ProjectId))
                .Select(existingKey => new ProjectKey
                {
                    FounderKey = existingKey.FounderKey,
                    FounderRecoveryKey = existingKey.FounderRecoveryKey,
                    Index = existingKey.Index,
                    WalletId = walletId,
                    ProjectId = existingKey.ProjectId,
                    NostrPubKey = existingKey.NostrPubKey,
                    CreatedAt = now,
                    UpdatedAt = now
                }).ToList();
            
            if (keysToAdd.Count != 0)
                await context.ProjectKeys.AddRangeAsync(keysToAdd);
        
            await context.SaveChangesAsync();
        
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save project keys: {ex.Message}");
        }
    }

    public async Task<Result<int>> HasProjectsKeys(Guid walletId)
    {
        try
        {
            var hasKeys = await context.ProjectKeys
                .CountAsync(pk => pk.WalletId == walletId);
            
            return Result.Success(hasKeys);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>($"Failed to check for cached project keys: {ex.Message}");
        }
    }
}
using Angor.Contexts.Data.Entities;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using NostrEvent = Nostr.Client.Messages.NostrEvent;

namespace Angor.Contexts.Data.Services;

public class UserEventService(AngorDbContext context, INostrService nostrService, ILogger<ProjectEventService> logger, ISerializer serializer) : IUserEventService
{
    public async Task<Result> PullAndSaveUserEventsAsync(params string[] pubkeys)
    {
        logger.LogInformation("Starting to pull user events with {PubKeyCount} specific public keys...", pubkeys.Length);

        try
        {
            // Step 1: Check which projects already exist in the database
            var existingProjectIds = await context.NostrUsers
                .Where(p => pubkeys.Contains(p.PubKey))
                .Select(p => p.PubKey)
                .ToListAsync();

            logger.LogInformation("Found {ExistingCount} projects already in database out of {TotalCount} requested", 
                existingProjectIds.Count, pubkeys.Length);

            // Step 2: Filter out event IDs that already have corresponding projects
            var missingEventIds = pubkeys.Except(existingProjectIds).ToArray();

            if (missingEventIds.Length == 0)
            {
                logger.LogInformation("All requested projects already exist in database");
                // Return existing projects
                return Result.Success();
            }
            
            // Get user events from Nostr relays
            var userResponses = await nostrService.GetEventsAsync(
                nameof(ProcessUserEvent), [NostrKind.Metadata], null, pubkeys);

            var users =  userResponses.Where(x => x.Event?.Content != null)
                .Select(x => ProcessUserEvent(x.Event!))
                .Where(x => x != null)
                .ToArray();
            
            var savedCount = await SaveUsersAsync(users!);
            
            logger.LogInformation($"Successfully processed {users.Length} user events, saved {savedCount} to database");
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pulling user events");
            return Result.Failure(ex.Message);;
        }
    }
    
    private NostrUser? ProcessUserEvent(NostrEvent responseEvent)
    {
        try
        {
            logger.LogDebug("Processing user event data");

            // Deserialize the event data
            if (responseEvent is not NostrMetadataEvent userInfo)
            {
                logger.LogWarning("Invalid or incomplete user event data");
                return null;
            }

            // Map UserInfo to User entity
            var user = new NostrUser
            {
                CreatedAt = responseEvent.CreatedAt!.Value,
                About = userInfo.Metadata!.About,
                DisplayName = userInfo.Metadata.DisplayName,
                Picture = userInfo.Metadata.Picture,
                ProfileEventId = responseEvent.Id!,
                Nip05 = userInfo.Metadata.Nip05,
                PubKey = userInfo.Pubkey!,
                UpdatedAt = DateTime.UtcNow, // Set to current time
                Website = userInfo.Metadata.Website,
                IsVerified = userInfo.IsSignatureValid()
            };

            return user;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing user event: {EventData}", serializer.Serialize(responseEvent));
            return null;
        }
    }
    
    private async Task<int> SaveUsersAsync(params NostrUser[]? users)
    {
        if (users == null || users.Length == 0)
            return 0;

        try
        {
            // Get all existing users in one query
            var pubKeys = users.Select(u => u.PubKey).ToArray();
            var existingUsers = await context.NostrUsers
                .Where(u => pubKeys.Contains(u.PubKey))
                .ToDictionaryAsync(u => u.PubKey);

            var usersToAdd = new List<NostrUser>();
            var usersToUpdate = new List<NostrUser>();

            // Categorize users for batch operations
            foreach (var user in users)
            {
                if (existingUsers.TryGetValue(user.PubKey, out var existingUser))
                {
                    // Update existing user properties
                    existingUser.About = user.About;
                    existingUser.DisplayName = user.DisplayName;
                    existingUser.Picture = user.Picture;
                    existingUser.Nip05 = user.Nip05;
                    existingUser.Website = user.Website;
                    existingUser.IsVerified = user.IsVerified;
                    existingUser.UpdatedAt = DateTime.UtcNow;
                
                    usersToUpdate.Add(existingUser);
                }
                else
                {
                    usersToAdd.Add(user);
                }
            }

            // Batch operations
            if (usersToAdd.Any())
            {
                await context.NostrUsers.AddRangeAsync(usersToAdd);
            }

            if (usersToUpdate.Any())
            {
                context.NostrUsers.UpdateRange(usersToUpdate);
            }

            // Single SaveChanges call for all operations
            await context.SaveChangesAsync();
        
            return usersToAdd.Count + usersToUpdate.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error batch saving {UserCount} users", users.Length);
            throw;
        }
    }

    
    private async Task<bool> SaveUserAsync(NostrUser user)
    {
        try
        {
            // Check if the user already exists in the database
            var existingUser = await context.NostrUsers.FindAsync(user.PubKey);

            if (existingUser != null)
            {
                // Update existing user
                existingUser.About = user.About;
                existingUser.DisplayName = user.DisplayName;
                existingUser.Picture = user.Picture;
                existingUser.Nip05 = user.Nip05;
                existingUser.Website = user.Website;
                existingUser.IsVerified = user.IsVerified;
                existingUser.UpdatedAt = DateTime.UtcNow;

                context.NostrUsers.Update(existingUser);
            }
            else
            {
                // Add new user
                await context.NostrUsers.AddAsync(user);
            }

            // Save changes to the database
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving user with PubKey: {PubKey}", user.PubKey);
            return false;
        }
    }
}
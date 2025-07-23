using Angor.Contexts.Data.Entities;
using Angor.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using NostrEvent = Nostr.Client.Messages.NostrEvent;

namespace Angor.Contexts.Data.Services;

public class UserEventService(AngorDbContext context, INostrService nostrService, ILogger<ProjectEventService> logger, ISerializer serializer) : IUserEventService
{
    public async Task<List<NostrUser>> PullAndSaveUserEventsAsync(params string[] pubkeys)
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
                return await context.NostrUsers
                    .Where(p => pubkeys.Contains(p.PubKey))
                    .ToListAsync();
            }
            
            // Get user events from Nostr relays
            var userResponses = await nostrService.GetEventsAsync(nameof(ProcessUserEvent), new[] { NostrKind.Metadata }, null, pubkeys);

            var users = new List<NostrUser>();

            // Process each user event
            foreach (var userResponse in userResponses)
            {
                if (userResponse?.Event?.Content == null)
                    continue;

                var user = ProcessUserEvent(userResponse.Event);
                if (user != null)
                {
                    users.Add(user);
                }
            }

            // Save users to database
            var savedCount = 0;
            foreach (var user in users)
            {
                var saved = await SaveUserAsync(user);
                if (saved) savedCount++;
            }

            logger.LogInformation($"Successfully processed {users.Count} user events, saved {savedCount} to database");
            return await context.NostrUsers
                .Where(p => pubkeys.Contains(p.PubKey))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pulling user events");
            throw;
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
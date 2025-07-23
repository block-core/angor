using Angor.Contexts.Data.Entities;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;
using NostrEvent = Nostr.Client.Messages.NostrEvent;

namespace Angor.Contexts.Data.Services;

public class ProjectEventService(AngorDbContext context, INostrService nostrService, ILogger<ProjectEventService> logger, ISerializer serializer) : IProjectEventService
{
    private readonly NostrKind angorProjectInfoKind = (NostrKind)3030; // Assuming 3030 is the kind for Angor project info

    public async Task<List<Project>> PullAndSaveProjectEventsAsync(params string[] eventIds)
    {
        logger.LogInformation("Starting to pull project events with {EventCount} specific event IDs...", eventIds.Length);
        
        try
        {
            if (eventIds == null || eventIds.Length == 0)
            {
                logger.LogWarning("No event IDs provided");
                return new List<Project>();
            }

            // Step 1: Check which projects already exist in the database
            var existingProjectIds = await context.Projects
                .Where(p => eventIds.Contains(p.ProjectInfoEventId))
                .Select(p => p.ProjectInfoEventId)
                .ToListAsync();

            logger.LogInformation("Found {ExistingCount} projects already in database out of {TotalCount} requested", 
                existingProjectIds.Count, eventIds.Length);

            // Step 2: Filter out event IDs that already have corresponding projects
            var missingEventIds = eventIds.Except(existingProjectIds).ToArray();

            if (missingEventIds.Length == 0)
            {
                logger.LogInformation("All requested projects already exist in database");
            
                // Return existing projects
                return await context.Projects
                    .Where(p => eventIds.Contains(p.ProjectInfoEventId))
                    .Include(p => p.NostrUser)
                    .Include(p => p.NostrEvent)
                    .Include(p => p.Stages)
                    .Include(p => p.SecretHashes)
                    .ToListAsync();
            }

            
            // Get events of kind 3030 from Nostr relays
            var eventResponses = await nostrService.GetEventsAsync(nameof(ProcessProjectEvent),
                [angorProjectInfoKind], null, null, missingEventIds);
            
            var projects = new List<Project>();
            
            // Process each event
            foreach (var eventResponse in eventResponses)
            {
                if (eventResponse?.Event?.Content == null) 
                    continue;
                
                var project = ProcessProjectEvent(eventResponse.Event);
                if (project != null)
                {
                    projects.Add(project);
                }
            }

            // Save projects to database
            var savedCount = 0;
            foreach (var project in projects)
            {
                var saved = await SaveProjectAsync(project);
                if (saved) savedCount++;
            }
            
            logger.LogInformation($"Successfully processed {projects.Count} project events, saved {savedCount} to database");
            
            // Step 5: Return all projects (existing + newly saved) with full includes
            var allProjects = await context.Projects
                .Where(p => eventIds.Contains(p.ProjectInfoEventId))
                .Include(p => p.NostrUser)
                .Include(p => p.NostrEvent)
                .Include(p => p.Stages)
                .Include(p => p.SecretHashes)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            logger.LogInformation("Returning {TotalCount} total projects ({ExistingCount} existing + {NewCount} new)", 
                allProjects.Count, existingProjectIds.Count, eventResponses.Count);

            return allProjects;

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pulling project events");
            throw;
        }
    }
    
    private Project? ProcessProjectEvent(NostrEvent responseEvent)
    {
        try
        {
            logger.LogDebug("Processing project event data");

            // Deserialize the event data
            var projectInfo = serializer.Deserialize<ProjectInfo>(responseEvent.Content);
            if (projectInfo == null || string.IsNullOrEmpty(projectInfo.ProjectIdentifier))
            {
                logger.LogWarning("Invalid or incomplete project event data");
                return null;
            }

            // Map ProjectInfo to Project entity
            var project = new Project
            {
                ProjectId = projectInfo.ProjectIdentifier,
                CreatedAt = responseEvent.CreatedAt!.Value,
                FundingStartDate = projectInfo.StartDate,
                FundingEndDate = projectInfo.EndDate,
                ExpiryDate = projectInfo.ExpiryDate,
                NostrPubKey = projectInfo.NostrPubKey,
                ProjectReceiveAddress = projectInfo.FounderKey,
                PenaltyDays = projectInfo.PenaltyDays,
                ProjectInfoEventId = responseEvent!.Id!,
                TargetAmount = projectInfo.TargetAmount,
                UpdatedAt = DateTime.UtcNow, // Set to current time
                Stages = projectInfo.Stages?.Select((s,i) => new ProjectStage
                {
                    ProjectId = projectInfo.ProjectIdentifier,
                    CreatedAt = responseEvent.CreatedAt!.Value,
                    StageIndex = i,
                    AmountToRelease = s.AmountToRelease,
                    ReleaseDate = s.ReleaseDate,
                }).ToList() ?? new List<ProjectStage>(),
                SecretHashes = projectInfo.ProjectSeeders.SecretHashes.Select(sh => new ProjectSecretHash
                {
                    ProjectId = projectInfo.ProjectIdentifier,
                    SecretHash = sh,
                    CreatedAt = responseEvent.CreatedAt!.Value,
                }).ToList(),
                LeadInvestorsThreshold = projectInfo.ProjectSeeders.Threshold,
                NostrEvent = new Entities.NostrEvent
                {
                    CreatedAt = responseEvent.CreatedAt!.Value,
                    Content = responseEvent.Content,
                    Id = responseEvent.Id,
                    PubKey = responseEvent.Pubkey!,
                    Kind = (int)responseEvent.Kind,
                    Signature = responseEvent.Sig,
                    Tags = responseEvent.Tags.Select(t => string.Join(",", t)).ToList()
                }
            };

            return project;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing project event: {EventData}", serializer.Serialize(responseEvent));
            return null;
        }
    }
    
    public async Task<bool> SaveProjectAsync(Project project)
    {
        try
        {
            if (project == null)
            {
                logger.LogError("Cannot save null project");
                return false;
            }
            // Check if the project already exists
            var existingProject = await context.Projects
                .Include(p => p.Stages)
                .Include(p => p.SecretHashes)
                .FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId);

            if (existingProject == null)
            {
                // Add new project
                context.Projects.Add(project);
                logger.LogInformation("Adding new project: {ProjectId}", project.ProjectId);
            }
            else
            {
                var newHashes = project.SecretHashes
                    .Where(sh => existingProject.SecretHashes.All(eh => eh.SecretHash != sh.SecretHash))
                    .ToList();
                
                 // Update related entities (SecretHashes)
                 if (newHashes.Count > 0)
                     await context.ProjectSecretHashes.AddRangeAsync(newHashes);

                logger.LogInformation("Updating existing project: {ProjectId}", project.ProjectId);
            }

            // Save changes to the database
            var result = await context.SaveChangesAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving project: {ProjectId}", project.ProjectId);
            return false;
        }
    }

    public async Task<IEnumerable<Project>> GetProjectsByIdsAsync(params string[] projectIds)
    {
        logger.LogInformation("Getting {Count} projects by IDs with all nested data", projectIds.Length);

        try
        {
            if (projectIds == null || projectIds.Length == 0)
            {
                logger.LogWarning("No project IDs provided");
                return new List<Project>();
            }

            var projects = await context.Projects
                .Where(p => projectIds.Contains(p.ProjectId))
                .Include(p => p.NostrUser) // Include the associated NostrUser
                .Include(p => p.NostrEvent) // Include the associated NostrEvent
                .Include(p => p.Stages) // Include all project stages
                .Include(p => p.SecretHashes) // Include all secret hashes
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            logger.LogInformation("Retrieved {Found} projects out of {Requested} requested IDs", 
                projects.Count, projectIds.Length);

            // Log missing projects for debugging
            var foundIds = projects.Select(p => p.ProjectId).ToHashSet();
            var missingIds = projectIds.Where(id => !foundIds.Contains(id)).ToArray();
            if (missingIds.Length > 0)
            {
                logger.LogWarning("Projects not found in database: {MissingIds}", 
                    string.Join(", ", missingIds));
            }

            return projects;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving projects by IDs: {ProjectIds}", 
                string.Join(", ", projectIds));
            return new List<Project>();
        }
    }

}
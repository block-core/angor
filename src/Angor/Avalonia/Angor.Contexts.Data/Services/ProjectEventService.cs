using Angor.Contexts.Data.Entities;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;
using NostrEvent = Nostr.Client.Messages.NostrEvent;

namespace Angor.Contexts.Data.Services;

public class ProjectEventService(AngorDbContext context, INostrService nostrService, ILogger<ProjectEventService> logger, ISerializer serializer) : IProjectEventService
{
    private readonly NostrKind angorProjectInfoKind = (NostrKind)3030; // Assuming 3030 is the kind for Angor project info

    public async Task<Result> PullAndSaveProjectEventsAsync(params string[] eventIds)
    {
        logger.LogInformation("Starting to pull project events with {EventCount} specific event IDs...", eventIds.Length);
        
        try
        {
            if (eventIds == null || eventIds.Length == 0)
            {
                logger.LogWarning("No event IDs provided");
                return Result.Success();
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
                return Result.Success();
            }

            
            // Get events of kind 3030 from Nostr relays
            var eventResponses = await nostrService.GetEventsAsync(nameof(ProcessProjectEvent),
                [angorProjectInfoKind], null, null, missingEventIds);
            
            var projects = eventResponses
                .Where(x => x.Event?.Content != null)
                .Select(x => ProcessProjectEvent(x.Event!))
                .Where(x => x != null)
                .ToArray();
            
            // Save projects to database
            var savedCount = await SaveProjectsAsync(projects!);
            
            logger.LogInformation($"Successfully processed {projects.Length} project events, saved {savedCount} to database");
            return Result.Success();

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pulling project events");
            return Result.Failure(ex.Message);
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
                    Id = responseEvent.Id!,
                    PubKey = responseEvent.Pubkey!,
                    Kind = (int)responseEvent.Kind,
                    Signature = responseEvent.Sig!,
                    Tags = responseEvent.Tags?.Select(tag => new NostrTag
                    {
                        Name = tag.TagIdentifier,
                        Content = tag.AdditionalData.ToList(),
                        EventId = responseEvent.Id!,
                    }).ToList() ?? null
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
    public async Task<int> SaveProjectsAsync(params Project[] projects)
    {
        if (projects == null || projects.Length == 0)
            return 0;

        try
        {
            // Get existing projects to determine updates vs inserts
            var projectIds = projects.Select(p => p.ProjectId).ToArray();
            var existingProjects = await context.Projects
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId);

            var projectsToAdd = new List<Project>();
            var projectsToUpdate = new List<Project>();

            // Use LINQ for transformation, foreach for processing
            foreach (var project in projects)
            {
                if (existingProjects.TryGetValue(project.ProjectId, out var existingProject))
                {
                    // Update existing project
                    // UpdateProjectProperties(existingProject, project);
                    // projectsToUpdate.Add(existingProject);
                    //TODO for now we only add new projects but need to check which properties can actually be updated
                }
                else
                {
                    // Add new project
                    projectsToAdd.Add(project);
                }
            }

            // Batch database operations
            if (projectsToAdd.Count > 0)
            {
                await context.Projects.AddRangeAsync(projectsToAdd);
            }

            await context.SaveChangesAsync();
        
            logger.LogInformation("Saved {AddCount} new projects, updated {UpdateCount} existing projects", 
                projectsToAdd.Count, projectsToUpdate.Count);

            return projectsToAdd.Count + projectsToUpdate.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving {ProjectCount} projects", projects.Length);
            throw;
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
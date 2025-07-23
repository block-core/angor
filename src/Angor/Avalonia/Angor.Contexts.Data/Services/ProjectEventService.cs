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
            // Get events of kind 3030 from Nostr relays
            var eventResponses = await nostrService.GetEventsAsync(nameof(ProcessProjectEvent), [], [angorProjectInfoKind], null, eventIds);
            
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
            return projects;
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
                LeadInvestorsThreshold = projectInfo.ProjectSeeders.Threshold
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
                     await context.ProjectSecretHashes.AddRangeAsync();

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

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        try
        {
            return await context.Projects
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving projects");
            return new List<Project>();
        }
    }
}
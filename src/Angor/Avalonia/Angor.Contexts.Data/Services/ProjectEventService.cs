using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Angor.Contexts.Data.Services;

public class ProjectEventService : IProjectEventService
{
    private readonly AngorDbContext _context;
    private readonly INostrService _nostrService;
    private readonly ILogger<ProjectEventService> _logger;

    public ProjectEventService(AngorDbContext context, INostrService nostrService, ILogger<ProjectEventService> logger)
    {
        _context = context;
        _nostrService = nostrService;
        _logger = logger;
    }

    public async Task<List<Project>> PullAndSaveProjectEventsAsync(params string[] eventIds)
    {
        _logger.LogInformation("Starting to pull project events with {EventCount} specific event IDs...", eventIds.Length);
        
        try
        {
            // Get events of kind 3030 from Nostr relays
            var eventResponses = await _nostrService.GetEventsByKindAsync(3030, eventIds);
            
            var projects = new List<Project>();
            
            // Process each event
            foreach (var eventResponse in eventResponses)
            {
                if (eventResponse?.Event?.Content != null)
                {
                    var project = await ProcessProjectEventAsync(eventResponse.Event.Content);
                    if (project != null)
                    {
                        projects.Add(project);
                    }
                }
            }

            // Save projects to database
            var savedCount = 0;
            foreach (var project in projects)
            {
                var saved = await SaveProjectAsync(project);
                if (saved) savedCount++;
            }
            
            _logger.LogInformation($"Successfully processed {projects.Count} project events, saved {savedCount} to database");
            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling project events");
            throw;
        }
    }

    public async Task<Project?> ProcessProjectEventAsync(string eventData)
    {
        try
        {
            // TODO: Implement event parsing logic
            // Parse the event data and create Project entity
            // This will depend on your event data format
            
            _logger.LogDebug("Processing project event data");
            return null; // Placeholder - implement based on your event structure
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing project event: {EventData}", eventData);
            return null;
        }
    }

    public async Task<bool> SaveProjectAsync(Project project)
    {
        try
        {
            // Check if project already exists
            var existingProject = await _context.Projects
                .FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId);

            if (existingProject == null)
            {
                _context.Projects.Add(project);
                _logger.LogInformation("Adding new project: {ProjectId}", project.ProjectId);
            }
            else
            {
                // Update existing project
                _context.Entry(existingProject).CurrentValues.SetValues(project);
                _logger.LogInformation("Updating existing project: {ProjectId}", project.ProjectId);
            }

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving project: {ProjectId}", project.ProjectId);
            return false;
        }
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        try
        {
            return await _context.Projects
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects");
            return new List<Project>();
        }
    }
}
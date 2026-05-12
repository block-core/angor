using System.ComponentModel;
using System.Text.Json;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using ModelContextProtocol.Server;

namespace Angor.Cli.McpTools;

[McpServerToolType]
public class ProjectTools(IProjectAppService projectService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description("List the latest projects. Returns project IDs and metadata.")]
    public async Task<string> ProjectList()
    {
        var result = await projectService.Latest(new LatestProjects.LatestProjectsRequest());
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get full details for a project by ID")]
    public async Task<string> ProjectGet(string projectId)
    {
        var result = await projectService.Get(new GetProject.GetProjectRequest(new ProjectId(projectId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Try to get a project by ID. Returns null if not found.")]
    public async Task<string> ProjectTryGet(string projectId)
    {
        var result = await projectService.TryGet(new TryGetProject.TryGetProjectRequest(new ProjectId(projectId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get project statistics (total invested, investor count, etc.)")]
    public async Task<string> ProjectStats(string projectId)
    {
        var result = await projectService.GetProjectStatistics(new ProjectId(projectId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get raw project info JSON from Nostr")]
    public async Task<string> ProjectInfoJson(string projectId)
    {
        var result = await projectService.GetProjectInfoJson(new ProjectId(projectId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Fetch project profile data (description, FAQ, members, media)")]
    public async Task<string> ProjectProfile(string projectId)
    {
        var result = await projectService.FetchProjectProfileData(new ProjectId(projectId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get relay URLs for a Nostr public key")]
    public async Task<string> ProjectRelays(string nostrPubKey)
    {
        var result = await projectService.GetRelaysForNpubAsync(nostrPubKey);
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }
}

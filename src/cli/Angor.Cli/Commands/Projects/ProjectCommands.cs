using System.CommandLine;
using System.Text.Json;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Cli.Commands.Projects;

public static class ProjectCommands
{
    public static Command Build(IServiceProvider services, JsonSerializerOptions jsonOptions)
    {
        var projectService = services.GetRequiredService<IProjectAppService>();

        var projectCommand = new Command("project", "Project browsing commands");

        projectCommand.AddCommand(BuildListCommand(projectService, jsonOptions));
        projectCommand.AddCommand(BuildGetCommand(projectService, jsonOptions));
        projectCommand.AddCommand(BuildTryGetCommand(projectService, jsonOptions));
        projectCommand.AddCommand(BuildStatsCommand(projectService, jsonOptions));
        projectCommand.AddCommand(BuildInfoJsonCommand(projectService, jsonOptions));
        projectCommand.AddCommand(BuildProfileCommand(projectService, jsonOptions));

        return projectCommand;
    }

    private static Command BuildListCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("list", "List latest projects") { jsonOption };
        cmd.SetHandler(async (bool json) =>
        {
            var result = await projectService.Latest(new LatestProjects.LatestProjectsRequest());
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
                return;
            }

            foreach (var project in result.Value.Projects)
            {
                Console.WriteLine($"  {project.Id}  {project.Name ?? "(no name)"}");
            }
        }, jsonOption);
        return cmd;
    }

    private static Command BuildGetCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var idOption = new Option<string>("--id", "Project ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("get", "Get project details") { idOption, jsonOption };
        cmd.SetHandler(async (string id, bool json) =>
        {
            var result = await projectService.Get(new GetProject.GetProjectRequest(new ProjectId(id)));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, idOption, jsonOption);
        return cmd;
    }

    private static Command BuildTryGetCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var idOption = new Option<string>("--id", "Project ID") { IsRequired = true };

        var cmd = new Command("try-get", "Try to get a project (returns null if not found)") { idOption };
        cmd.SetHandler(async (string id) =>
        {
            var result = await projectService.TryGet(new TryGetProject.TryGetProjectRequest(new ProjectId(id)));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, idOption);
        return cmd;
    }

    private static Command BuildStatsCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var idOption = new Option<string>("--id", "Project ID") { IsRequired = true };

        var cmd = new Command("stats", "Get project statistics") { idOption };
        cmd.SetHandler(async (string id) =>
        {
            var result = await projectService.GetProjectStatistics(new ProjectId(id));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, idOption);
        return cmd;
    }

    private static Command BuildInfoJsonCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var idOption = new Option<string>("--id", "Project ID") { IsRequired = true };

        var cmd = new Command("info-json", "Get raw project info JSON") { idOption };
        cmd.SetHandler(async (string id) =>
        {
            var result = await projectService.GetProjectInfoJson(new ProjectId(id));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, idOption);
        return cmd;
    }

    private static Command BuildProfileCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var idOption = new Option<string>("--id", "Project ID") { IsRequired = true };

        var cmd = new Command("profile", "Fetch project profile data") { idOption };
        cmd.SetHandler(async (string id) =>
        {
            var result = await projectService.FetchProjectProfileData(new ProjectId(id));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, idOption);
        return cmd;
    }
}

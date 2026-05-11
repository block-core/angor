using System.Text.Json;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Services;
using Angor.Primitives;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class GetProjectInfoJson
{
    public record GetProjectInfoJsonRequest(ProjectId ProjectId) : IRequest<Result<GetProjectInfoJsonResponse>>;

    public record GetProjectInfoJsonResponse(string Json);

    public class GetProjectInfoJsonHandler(IProjectService projectService)
        : IRequestHandler<GetProjectInfoJsonRequest, Result<GetProjectInfoJsonResponse>>
    {
        private static readonly JsonSerializerOptions IndentedOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new Angor.Shared.Utilities.UnixDateTimeConverter() }
        };

        public async Task<Result<GetProjectInfoJsonResponse>> Handle(
            GetProjectInfoJsonRequest request, CancellationToken cancellationToken)
        {
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
                return Result.Failure<GetProjectInfoJsonResponse>(projectResult.Error);

            var projectInfo = projectResult.Value.ToProjectInfo();
            var json = JsonSerializer.Serialize(projectInfo, IndentedOptions);
            return Result.Success(new GetProjectInfoJsonResponse(json));
        }
    }
}

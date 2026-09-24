using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Projects.Operations;
using App.UI.Sections.FindProjects;
using Avalonia.Headless.XUnit;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace App.Test.Integration.LayoutRegression;

public class ProjectRecoveryTests
{
    [AvaloniaFact]
    public async Task Failed_refresh_keeps_cached_projects_and_successful_retry_clears_error()
    {
        var service = new Mock<IProjectAppService>();
        service.Setup(x => x.Latest(It.IsAny<LatestProjects.LatestProjectsRequest>()))
            .ReturnsAsync(Result.Failure<LatestProjects.LatestProjectsResponse>("offline"));
        using var vm = ActivatorUtilities.CreateInstance<FindProjectsViewModel>(global::App.App.Services, service.Object);
        var savedProject = new ProjectItemViewModel { ProjectName = "Saved project" };
        vm.Projects.Add(savedProject);

        await vm.LoadProjectsFromSdkAsync();

        vm.HasLoadError.Should().BeTrue();
        vm.IsEmpty.Should().BeFalse();
        vm.Projects.Should().Contain(savedProject);
        vm.LoadErrorMessage.Should().Contain("saved projects");
        vm.IsInitialLoad.Should().BeFalse();
        vm.IsLoading.Should().BeFalse();

        service.Setup(x => x.Latest(It.IsAny<LatestProjects.LatestProjectsRequest>()))
            .ReturnsAsync(Result.Success(new LatestProjects.LatestProjectsResponse(Array.Empty<ProjectDto>())));
        await vm.LoadProjectsFromSdkAsync();

        vm.HasLoadError.Should().BeFalse();
        vm.IsEmpty.Should().BeTrue();
    }
}

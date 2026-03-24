using Angor.Sdk.Funding.Projects.Dtos;
using Avalonia2.UI.Sections.MyProjects;
using Avalonia2.UI.Sections.MyProjects.Deploy;

namespace Avalonia2.Tests.ViewModels;

public class MyProjectsViewModelTests
{
    private readonly Mock<IProjectAppService> _projectAppService = new();
    private readonly Mock<IWalletAppService> _walletAppService = new();
    private readonly Mock<IFounderAppService> _founderAppService = new();

    private MyProjectsViewModel CreateVm()
    {
        var manageFactory = new Func<MyProjectItemViewModel, ManageProjectViewModel>(
            project => new ManageProjectViewModel(project, _founderAppService.Object));

        var createProjectVm = new CreateProjectViewModel(
            new DeployFlowViewModel(_walletAppService.Object, _projectAppService.Object, _founderAppService.Object));

        return new MyProjectsViewModel(
            _projectAppService.Object,
            _walletAppService.Object,
            manageFactory,
            createProjectVm);
    }

    [Fact]
    public async Task LoadFounderProjects_NoWallets_DoesNotCrash()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("no wallets"));

        var vm = CreateVm();
        await vm.LoadFounderProjectsAsync();

        vm.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadFounderProjects_WithProjects_PopulatesList()
    {
        var walletId = new WalletId("w1");
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Success<IEnumerable<WalletMetadata>>(
                new[] { new WalletMetadata("Wallet 1", walletId) }));

        var projectDto = new ProjectDto
        {
            Name = "My Funded Project",
            ShortDescription = "Description",
            TargetAmount = 100_000_000,
            ProjectType = Angor.Shared.Models.ProjectType.Invest,
            FundingStartDate = new DateTime(2025, 1, 1),
            FundingEndDate = new DateTime(2025, 12, 31),
            PenaltyDuration = TimeSpan.FromDays(30),
            Id = new ProjectId("proj1")
        };

        _projectAppService.Setup(x => x.GetFounderProjects(walletId))
            .ReturnsAsync(Result.Success(
                new Angor.Sdk.Funding.Projects.Operations.GetFounderProjects.GetFounderProjectsResponse(
                    new[] { projectDto })));

        var vm = CreateVm();
        await vm.LoadFounderProjectsAsync();

        vm.Projects.Should().HaveCount(1);
        vm.HasProjects.Should().BeTrue();
        vm.Projects[0].Name.Should().Be("My Funded Project");
        vm.Projects[0].ProjectType.Should().Be("investment");
        vm.Projects[0].TargetAmount.Should().Be("1.00000");
        vm.Projects[0].ProjectIdentifier.Should().Be("proj1");
        vm.Projects[0].OwnerWalletId.Should().Be("w1");
    }

    [Fact]
    public void LaunchCreateWizard_SetsFlag()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();

        vm.LaunchCreateWizard();

        vm.ShowCreateWizard.Should().BeTrue();
    }

    [Fact]
    public void CloseCreateWizard_ClearsFlag()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        vm.LaunchCreateWizard();

        vm.CloseCreateWizard();

        vm.ShowCreateWizard.Should().BeFalse();
    }

    [Fact]
    public void OnProjectDeployed_AddsToList()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        vm.CreateProjectVm.ProjectName = "New Project";
        vm.CreateProjectVm.ProjectAbout = "About my project";
        vm.CreateProjectVm.ProjectType = "investment";
        vm.CreateProjectVm.TargetAmount = "1.00000";
        vm.CreateProjectVm.StartDate = "2025-01-01";

        vm.OnProjectDeployed(vm.CreateProjectVm);

        vm.Projects.Should().HaveCount(1);
        vm.Projects[0].Name.Should().Be("New Project");
        vm.Projects[0].Description.Should().Be("About my project");
        vm.HasProjects.Should().BeTrue();
    }

    [Fact]
    public void TotalRaised_SumsProjectRaised()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        vm.Projects.Add(new MyProjectItemViewModel { Raised = "0.50000" });
        vm.Projects.Add(new MyProjectItemViewModel { Raised = "1.25000" });

        vm.TotalRaised.Should().Be("1.75000");
    }

    [Fact]
    public void ClearProjects_EmptiesList()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        vm.Projects.Add(new MyProjectItemViewModel { Name = "Test" });

        vm.ClearProjects();

        vm.Projects.Should().BeEmpty();
        vm.HasProjects.Should().BeFalse();
    }

    [Fact]
    public void OpenManageProject_CreatesManageVm()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));
        _founderAppService.Setup(x => x.GetClaimableTransactions(It.IsAny<Angor.Sdk.Funding.Founder.Operations.GetClaimableTransactions.GetClaimableTransactionsRequest>()))
            .ReturnsAsync(Result.Failure<Angor.Sdk.Funding.Founder.Operations.GetClaimableTransactions.GetClaimableTransactionsResponse>("err"));

        var vm = CreateVm();
        var project = new MyProjectItemViewModel { Name = "Managed" };

        vm.OpenManageProject(project);

        vm.SelectedManageProject.Should().NotBeNull();
        vm.SelectedManageProject!.Project.Should().BeSameAs(project);
    }

    [Fact]
    public void CloseManageProject_ClearsSelection()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();

        vm.CloseManageProject();

        vm.SelectedManageProject.Should().BeNull();
    }

    [Fact]
    public async Task LoadFounderProjects_MapsFundType()
    {
        var walletId = new WalletId("w1");
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Success<IEnumerable<WalletMetadata>>(
                new[] { new WalletMetadata("W1", walletId) }));

        _projectAppService.Setup(x => x.GetFounderProjects(walletId))
            .ReturnsAsync(Result.Success(
                new Angor.Sdk.Funding.Projects.Operations.GetFounderProjects.GetFounderProjectsResponse(
                    new[] { new ProjectDto
                    {
                        Name = "Fund Project",
                        ProjectType = Angor.Shared.Models.ProjectType.Fund,
                        TargetAmount = 50_000_000,
                        FundingStartDate = DateTime.UtcNow,
                        FundingEndDate = DateTime.UtcNow.AddMonths(6),
                        PenaltyDuration = TimeSpan.FromDays(30),
                        Id = new ProjectId("fp1")
                    }})));

        var vm = CreateVm();
        await vm.LoadFounderProjectsAsync();

        vm.Projects[0].ProjectType.Should().Be("fund");
    }
}

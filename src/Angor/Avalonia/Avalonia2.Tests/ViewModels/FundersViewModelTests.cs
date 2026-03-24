using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Dtos;
using Avalonia2.UI.Sections.Funders;

namespace Avalonia2.Tests.ViewModels;

public class FundersViewModelTests
{
    private readonly Mock<IFounderAppService> _founderAppService = new();
    private readonly Mock<IProjectAppService> _projectAppService = new();
    private readonly Mock<IWalletAppService> _walletAppService = new();
    private readonly SignatureStore _signatureStore = new();

    private FundersViewModel CreateVm()
    {
        return new FundersViewModel(
            _founderAppService.Object,
            _projectAppService.Object,
            _walletAppService.Object,
            _signatureStore);
    }

    [Fact]
    public async Task LoadInvestmentRequests_NoWallets_StaysEmpty()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        await vm.LoadInvestmentRequestsAsync();

        vm.HasFunders.Should().BeFalse();
    }

    [Fact]
    public void SetFilter_UpdatesFilteredList()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        _signatureStore.AddSignature("p1", "A", "1.0"); // waiting
        _signatureStore.AddSignature("p2", "B", "0.005"); // auto-approved

        vm.SetFilter(SignatureStatus.Waiting.ToLowerString());

        vm.FilteredSignatures.Should().HaveCount(1);
        vm.FilteredSignatures[0].ProjectTitle.Should().Be("A");
    }

    [Fact]
    public void SetFilter_Approved_ShowsApproved()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        _signatureStore.AddSignature("p1", "A", "1.0");
        _signatureStore.AddSignature("p2", "B", "0.005"); // auto-approved

        vm.SetFilter(SignatureStatus.Approved.ToLowerString());

        vm.FilteredSignatures.Should().HaveCount(1);
        vm.FilteredSignatures[0].ProjectTitle.Should().Be("B");
    }

    [Fact]
    public void ApproveSignature_SharedStore_ApprovesAndRefilters()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        var sig = _signatureStore.AddSignature("p1", "Test", "1.0");

        vm.SetFilter(SignatureStatus.Waiting.ToLowerString());
        vm.FilteredSignatures.Should().HaveCount(1);

        vm.ApproveSignature(sig.Id);

        vm.FilteredSignatures.Should().BeEmpty(); // approved items leave waiting filter
        vm.WaitingCount.Should().Be(0);
        vm.ApprovedCount.Should().Be(1);
    }

    [Fact]
    public void RejectSignature_SharedStore_RejectsAndRefilters()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        var sig = _signatureStore.AddSignature("p1", "Test", "1.0");

        vm.RejectSignature(sig.Id);

        vm.RejectedCount.Should().Be(1);
        vm.HasRejected.Should().BeTrue();
    }

    [Fact]
    public void ApproveAll_ApprovesAllWaiting()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        _signatureStore.AddSignature("p1", "A", "1.0");
        _signatureStore.AddSignature("p2", "B", "2.0");

        vm.ApproveAll();

        vm.WaitingCount.Should().Be(0);
        vm.ApprovedCount.Should().Be(2);
    }

    [Fact]
    public void ToggleExpanded_TogglesState()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();

        vm.IsExpanded(42).Should().BeFalse();

        vm.ToggleExpanded(42);
        vm.IsExpanded(42).Should().BeTrue();

        vm.ToggleExpanded(42);
        vm.IsExpanded(42).Should().BeFalse();
    }

    [Fact]
    public void Counts_ReflectCurrentState()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();

        var sig1 = _signatureStore.AddSignature("p1", "A", "1.0"); // waiting
        _signatureStore.AddSignature("p2", "B", "0.005"); // auto-approved
        var sig3 = _signatureStore.AddSignature("p3", "C", "2.0"); // waiting

        // Force re-read by setting filter
        vm.SetFilter(SignatureStatus.Waiting.ToLowerString());

        vm.WaitingCount.Should().Be(2);
        vm.ApprovedCount.Should().Be(1);
        vm.RejectedCount.Should().Be(0);

        _signatureStore.Reject(sig1.Id);
        vm.SetFilter(SignatureStatus.Waiting.ToLowerString()); // trigger refresh

        vm.WaitingCount.Should().Be(1);
        vm.RejectedCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_UnsubscribesFromStore()
    {
        _walletAppService.Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Failure<IEnumerable<WalletMetadata>>("err"));

        var vm = CreateVm();
        vm.Dispose();

        // Adding to store after dispose should not cause errors
        _signatureStore.AddSignature("p1", "Test", "1.0");
    }

    [Fact]
    public void SignatureRequestViewModel_FromShared_MapsFields()
    {
        var shared = new SharedSignature
        {
            Id = 42,
            ProjectTitle = "Test Project",
            Amount = "0.5",
            Currency = "BTC",
            Date = "Jan 01, 2025",
            Time = "12:00",
            Status = SignatureStatus.Waiting.ToLowerString(),
            Npub = "npub1test",
            HasMessages = true
        };

        var vm = SignatureRequestViewModel.FromShared(shared);

        vm.Id.Should().Be(42);
        vm.ProjectTitle.Should().Be("Test Project");
        vm.Amount.Should().Be("0.5");
        vm.IsWaiting.Should().BeTrue();
        vm.IsApproved.Should().BeFalse();
        vm.HasMessages.Should().BeTrue();
    }
}

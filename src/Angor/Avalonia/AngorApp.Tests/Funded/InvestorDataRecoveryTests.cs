using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Investor.Operations;
using AngorApp.Model.Contracts.Wallet;
using AngorApp.Model.Funded.Fund.Model;
using AngorApp.Model.Funded.Investment.Model;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;

namespace AngorApp.Tests.Funded;

public class InvestorDataRecoveryTests
{
    [Fact]
    public async Task InvestmentInvestorData_refresh_should_keep_previous_recovery_when_recovery_status_fails()
    {
        const string projectId = "project-1";
        var walletId = new WalletId("wallet-1");

        var service = BuildInvestmentService(projectId);
        var walletContext = BuildWalletContext(walletId);

        using var sut = new InvestmentInvestorData(CreateInvestedProjectDto(projectId), service.Object, walletContext.Object);
        RecoveryState? latest = null;
        using var subscription = sut.RecoveryState.Subscribe(state => latest = state);

        sut.Refresh.Execute(null);
        await WaitUntil(() => latest?.HasUnspentItems == true);
        latest.Should().NotBeNull();
        latest!.HasUnspentItems.Should().BeTrue();

        sut.Refresh.Execute(null);
        await WaitUntil(() => service.Invocations.Count(invocation => invocation.Method.Name == nameof(IInvestmentAppService.GetRecoveryStatus)) >= 2);
        latest!.HasUnspentItems.Should().BeTrue();
    }

    [Fact]
    public async Task FundInvestorData_refresh_should_keep_previous_recovery_when_recovery_status_fails()
    {
        const string projectId = "project-1";
        var walletId = new WalletId("wallet-1");

        var service = BuildInvestmentService(projectId);
        var walletContext = BuildWalletContext(walletId);

        using var sut = new FundInvestorData(CreateInvestedProjectDto(projectId), service.Object, walletContext.Object);
        RecoveryState? latest = null;
        using var subscription = sut.RecoveryState.Subscribe(state => latest = state);

        sut.Refresh.Execute(null);
        await WaitUntil(() => latest?.HasUnspentItems == true);
        latest.Should().NotBeNull();
        latest!.HasUnspentItems.Should().BeTrue();

        sut.Refresh.Execute(null);
        await WaitUntil(() => service.Invocations.Count(invocation => invocation.Method.Name == nameof(IInvestmentAppService.GetRecoveryStatus)) >= 2);
        latest!.HasUnspentItems.Should().BeTrue();
    }

    private static Mock<IInvestmentAppService> BuildInvestmentService(string projectId)
    {
        var service = new Mock<IInvestmentAppService>();

        service.SetupSequence(x => x.GetInvestments(It.IsAny<GetInvestments.GetInvestmentsRequest>()))
            .ReturnsAsync(Result.Success(new GetInvestments.GetInvestmentsResponse(new[] { CreateInvestedProjectDto(projectId) })))
            .ReturnsAsync(Result.Success(new GetInvestments.GetInvestmentsResponse(new[] { CreateInvestedProjectDto(projectId) })));

        service.SetupSequence(x => x.GetRecoveryStatus(It.IsAny<GetRecoveryStatus.GetRecoveryStatusRequest>()))
            .ReturnsAsync(Result.Success(new GetRecoveryStatus.GetRecoveryStatusResponse(new InvestorProjectRecoveryDto
            {
                HasUnspentItems = true,
                HasSpendableItemsInPenalty = false,
                HasReleaseSignatures = false,
                EndOfProject = false,
                IsAboveThreshold = true,
            })))
            .ReturnsAsync(Result.Failure<GetRecoveryStatus.GetRecoveryStatusResponse>("temporary backend failure"));

        return service;
    }

    private static Mock<IWalletContext> BuildWalletContext(WalletId walletId)
    {
        var wallet = new Mock<IWallet>();
        wallet.SetupGet(x => x.Id).Returns(walletId);

        var walletContext = new Mock<IWalletContext>();
        walletContext.Setup(x => x.TryGet()).ReturnsAsync(Maybe<IWallet>.From(wallet.Object));

        return walletContext;
    }

    private static InvestedProjectDto CreateInvestedProjectDto(string projectId)
    {
        return new InvestedProjectDto
        {
            Id = projectId,
            FounderStatus = FounderStatus.Approved,
            LogoUri = new Uri("https://example.com/logo.png"),
            Target = new Amount(1_000_000),
            Investment = new Amount(250_000),
            Name = "Project",
            Raised = new Amount(750_000),
            Description = "Description",
            InRecovery = new Amount(0),
            InvestmentStatus = InvestmentStatus.Invested,
            InvestmentId = "investment-1",
            RequestedOn = DateTimeOffset.UtcNow,
        };
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 1000)
    {
        var start = DateTimeOffset.UtcNow;
        while (!condition())
        {
            if ((DateTimeOffset.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(10);
        }
    }
}

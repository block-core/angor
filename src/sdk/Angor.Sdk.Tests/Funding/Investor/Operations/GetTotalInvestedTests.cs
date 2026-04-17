using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class GetTotalInvestedTests
{
    private readonly Mock<IGenericDocumentCollection<InvestmentRecordsDocument>> _mockDocumentCollection;
    private readonly GetTotalInvested.GetTotalInvestedHandler _sut;

    public GetTotalInvestedTests()
    {
        _mockDocumentCollection = new Mock<IGenericDocumentCollection<InvestmentRecordsDocument>>();
        _sut = new GetTotalInvested.GetTotalInvestedHandler(_mockDocumentCollection.Object);
    }

    [Fact]
    public async Task Handle_WhenDocumentNotFound_ReturnsZero()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetTotalInvested.GetTotalInvestedRequest(walletId);

        _mockDocumentCollection
            .Setup(x => x.FindByIdAsync(walletId.Value))
            .ReturnsAsync(Result.Failure<InvestmentRecordsDocument>("Not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInvestedSats.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenDocumentIsNull_ReturnsZero()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetTotalInvested.GetTotalInvestedRequest(walletId);

        _mockDocumentCollection
            .Setup(x => x.FindByIdAsync(walletId.Value))
            .ReturnsAsync(Result.Success<InvestmentRecordsDocument>(null!));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInvestedSats.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenInvestmentsListIsNull_ReturnsZero()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetTotalInvested.GetTotalInvestedRequest(walletId);

        _mockDocumentCollection
            .Setup(x => x.FindByIdAsync(walletId.Value))
            .ReturnsAsync(Result.Success(new InvestmentRecordsDocument
            {
                WalletId = walletId.Value,
                Investments = null!
            }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInvestedSats.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenInvestmentsListIsEmpty_ReturnsZero()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetTotalInvested.GetTotalInvestedRequest(walletId);

        _mockDocumentCollection
            .Setup(x => x.FindByIdAsync(walletId.Value))
            .ReturnsAsync(Result.Success(new InvestmentRecordsDocument
            {
                WalletId = walletId.Value,
                Investments = new List<InvestmentRecord>()
            }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInvestedSats.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenSingleInvestment_ReturnsTotalSats()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetTotalInvested.GetTotalInvestedRequest(walletId);

        _mockDocumentCollection
            .Setup(x => x.FindByIdAsync(walletId.Value))
            .ReturnsAsync(Result.Success(new InvestmentRecordsDocument
            {
                WalletId = walletId.Value,
                Investments = new List<InvestmentRecord>
                {
                    new InvestmentRecord
                    {
                        ProjectIdentifier = "project-1",
                        InvestedAmountSats = 500_000
                    }
                }
            }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInvestedSats.Should().Be(500_000);
    }

    [Fact]
    public async Task Handle_WhenMultipleInvestments_SumsAllAmounts()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetTotalInvested.GetTotalInvestedRequest(walletId);

        _mockDocumentCollection
            .Setup(x => x.FindByIdAsync(walletId.Value))
            .ReturnsAsync(Result.Success(new InvestmentRecordsDocument
            {
                WalletId = walletId.Value,
                Investments = new List<InvestmentRecord>
                {
                    new InvestmentRecord { ProjectIdentifier = "project-1", InvestedAmountSats = 100_000 },
                    new InvestmentRecord { ProjectIdentifier = "project-2", InvestedAmountSats = 250_000 },
                    new InvestmentRecord { ProjectIdentifier = "project-3", InvestedAmountSats = 150_000 }
                }
            }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInvestedSats.Should().Be(500_000);
    }

    [Fact]
    public async Task Handle_UsesWalletIdValueAsDocumentKey()
    {
        // Arrange
        var walletId = new WalletId("specific-wallet-id");
        var request = new GetTotalInvested.GetTotalInvestedRequest(walletId);

        _mockDocumentCollection
            .Setup(x => x.FindByIdAsync("specific-wallet-id"))
            .ReturnsAsync(Result.Failure<InvestmentRecordsDocument>("Not found"));

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockDocumentCollection.Verify(x => x.FindByIdAsync("specific-wallet-id"), Times.Once);
    }
}

using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Services;
using FluentAssertions;
using Moq;

namespace Angor.Sdk.Tests.Funding.Founder;

public class PublishFounderTransactionTests
{
    private readonly Mock<IIndexerService> _mockIndexerService;
    private readonly PublishFounderTransaction.Handler _sut;

    public PublishFounderTransactionTests()
    {
        _mockIndexerService = new Mock<IIndexerService>();
        _sut = new PublishFounderTransaction.Handler(_mockIndexerService.Object, new Mock<Microsoft.Extensions.Logging.ILogger<PublishFounderTransaction.Handler>>().Object);
    }

    [Fact]
    public async Task Handle_WhenSignedTxHexIsEmpty_ReturnsFailure()
    {
        // Arrange
        var request = new PublishFounderTransaction.PublishFounderTransactionRequest(
            new TransactionDraft
            {
                SignedTxHex = "",
                TransactionId = "txid123",
                TransactionFee = new Amount(1000)
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Transaction signature cannot be empty");
        _mockIndexerService.Verify(x => x.PublishTransactionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSignedTxHexIsNull_ReturnsFailure()
    {
        // Arrange
        var request = new PublishFounderTransaction.PublishFounderTransactionRequest(
            new TransactionDraft
            {
                SignedTxHex = null!,
                TransactionId = "txid123",
                TransactionFee = new Amount(1000)
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Transaction signature cannot be empty");
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_ReturnsFailure()
    {
        // Arrange
        var request = new PublishFounderTransaction.PublishFounderTransactionRequest(
            new TransactionDraft
            {
                SignedTxHex = "0200000001abcdef...",
                TransactionId = "txid123",
                TransactionFee = new Amount(1000)
            });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.Handle(request, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Operation was cancelled");
        _mockIndexerService.Verify(x => x.PublishTransactionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIndexerReturnsError_ReturnsFailure()
    {
        // Arrange
        var signedHex = "0200000001abcdef...";
        var request = new PublishFounderTransaction.PublishFounderTransactionRequest(
            new TransactionDraft
            {
                SignedTxHex = signedHex,
                TransactionId = "txid123",
                TransactionFee = new Amount(1000)
            });

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync(signedHex))
            .ReturnsAsync("Transaction rejected: insufficient fee");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to publish founder transaction");
    }

    [Fact]
    public async Task Handle_WhenIndexerPublishesSuccessfully_ReturnsTransactionId()
    {
        // Arrange
        var signedHex = "0200000001abcdef...";
        var transactionId = "abc123def456";
        var request = new PublishFounderTransaction.PublishFounderTransactionRequest(
            new TransactionDraft
            {
                SignedTxHex = signedHex,
                TransactionId = transactionId,
                TransactionFee = new Amount(1000)
            });

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync(signedHex))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TransactionId.Should().Be(transactionId);
        _mockIndexerService.Verify(x => x.PublishTransactionAsync(signedHex), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenIndexerReturnsEmptyString_ReturnsSuccess()
    {
        // Arrange
        var request = new PublishFounderTransaction.PublishFounderTransactionRequest(
            new TransactionDraft
            {
                SignedTxHex = "0200000001abcdef...",
                TransactionId = "txid123",
                TransactionFee = new Amount(1000)
            });

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync(It.IsAny<string>()))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TransactionId.Should().Be("txid123");
    }
}

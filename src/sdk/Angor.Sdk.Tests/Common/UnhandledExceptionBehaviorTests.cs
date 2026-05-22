using Angor.Sdk.Common.MediatR;
using CSharpFunctionalExtensions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Angor.Sdk.Tests.Common;

public class UnhandledExceptionBehaviorTests
{
    // --- Result (non-generic) ---

    public record PlainRequest : IRequest<Result>;

    [Fact]
    public async Task Handle_WhenHandlerThrows_ReturnsFailureResult()
    {
        // Arrange
        var sut = new UnhandledExceptionBehavior<PlainRequest, Result>(
            NullLogger<UnhandledExceptionBehavior<PlainRequest, Result>>.Instance);

        RequestHandlerDelegate<Result> next = _ => throw new InvalidOperationException("boom");

        // Act
        var result = await sut.Handle(new PlainRequest(), next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("boom");
    }

    // --- Result<T> (generic) ---

    public record TypedRequest : IRequest<Result<string>>;

    [Fact]
    public async Task Handle_WhenHandlerThrows_ReturnsFailureResultOfT()
    {
        // Arrange
        var sut = new UnhandledExceptionBehavior<TypedRequest, Result<string>>(
            NullLogger<UnhandledExceptionBehavior<TypedRequest, Result<string>>>.Instance);

        RequestHandlerDelegate<Result<string>> next = _ => throw new InvalidOperationException("typed boom");

        // Act
        var result = await sut.Handle(new TypedRequest(), next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("typed boom");
    }

    // --- Result<T> with complex payload ---

    public record ComplexPayload(int Id, string Name);
    public record ComplexRequest : IRequest<Result<ComplexPayload>>;

    [Fact]
    public async Task Handle_WhenHandlerThrows_ReturnsFailureResultOfComplexType()
    {
        // Arrange
        var sut = new UnhandledExceptionBehavior<ComplexRequest, Result<ComplexPayload>>(
            NullLogger<UnhandledExceptionBehavior<ComplexRequest, Result<ComplexPayload>>>.Instance);

        RequestHandlerDelegate<Result<ComplexPayload>> next = _ => throw new Exception("complex boom");

        // Act
        var result = await sut.Handle(new ComplexRequest(), next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("complex boom");
    }

    // --- OperationCanceledException is re-thrown ---

    [Fact]
    public async Task Handle_WhenCancelled_Rethrows()
    {
        // Arrange
        var sut = new UnhandledExceptionBehavior<PlainRequest, Result>(
            NullLogger<UnhandledExceptionBehavior<PlainRequest, Result>>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        RequestHandlerDelegate<Result> next = _ => throw new OperationCanceledException(cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.Handle(new PlainRequest(), next, cts.Token));
    }

    // --- Success path passes through ---

    [Fact]
    public async Task Handle_WhenHandlerSucceeds_ReturnsResult()
    {
        // Arrange
        var sut = new UnhandledExceptionBehavior<TypedRequest, Result<string>>(
            NullLogger<UnhandledExceptionBehavior<TypedRequest, Result<string>>>.Instance);

        RequestHandlerDelegate<Result<string>> next = _ => Task.FromResult(Result.Success("hello"));

        // Act
        var result = await sut.Handle(new TypedRequest(), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }
}

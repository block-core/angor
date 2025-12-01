using Angor.Contexts.CrossCutting.MediatR;
using CSharpFunctionalExtensions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Contexts.Funding.Tests;

public class UnhandledExceptionBehaviorTests
{
    private readonly IMediator mediator;

    public UnhandledExceptionBehaviorTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(UnhandledExceptionBehaviorTests).Assembly);
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
        });
        services.AddTransient<IRequestHandler<BoomRequest, Result<int>>, BoomHandler>();

        mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task When_handler_throws_returns_failure_result_with_prefix()
    {
        var result = await mediator.Send(new BoomRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("Angor API failed:");
    }

    public record BoomRequest : IRequest<Result<int>>;

    private class BoomHandler : IRequestHandler<BoomRequest, Result<int>>
    {
        public Task<Result<int>> Handle(BoomRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }

    [Fact]
    public async Task When_handler_returning_Result_throws_returns_failure_result_with_prefix()
    {
        var result = await mediator.Send(new BoomRequestResult());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("Angor API failed:");
    }

    public record BoomRequestResult : IRequest<Result>;

    private class BoomHandlerResult : IRequestHandler<BoomRequestResult, Result>
    {
        public Task<Result> Handle(BoomRequestResult request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }
}

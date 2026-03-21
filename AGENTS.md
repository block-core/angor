# AGENTS.md - Angor Repository Guide

## Project Overview

Angor is a Bitcoin investment platform with two frontends:
- **Avalonia desktop/mobile app** (primary, .NET 9) in `src/Angor/Avalonia/`
- **Blazor WASM web app** (legacy, .NET 8) in `src/Angor/Client/`
- **Shared library** (.NET 8) in `src/Angor/Shared/`

## Build Commands

```bash
# Avalonia desktop app (primary solution)
dotnet build src/Angor/Avalonia/Angor.Avalonia.sln

# Blazor web app (legacy solution)
dotnet build src/Angor.sln

# Single project build
dotnet build src/Angor/Avalonia/AngorApp.Desktop/AngorApp.Desktop.csproj
```

## Test Commands

All test projects use **xUnit** with **FluentAssertions** and **Moq**.

```bash
# Run all SDK tests (net9.0)
dotnet test src/Angor/Avalonia/Angor.Sdk.Tests/Angor.Sdk.Tests.csproj

# Run legacy shared tests (net8.0)
dotnet test src/Angor.Test/Angor.Test.csproj

# Run Avalonia model tests (net9.0)
dotnet test src/Angor/Avalonia/AngorApp.Tests/AngorApp.Tests.csproj

# Run a single test by fully qualified name
dotnet test --filter "FullyQualifiedName=Angor.Sdk.Tests.Funding.Founder.FounderAppServiceTests.MethodName"

# Run a single test by display name
dotnet test --filter "DisplayName~GetProjectInvestmentsHandler_WhenProjectNotFound"

# Run all tests in a single class
dotnet test --filter "ClassName=Angor.Sdk.Tests.Funding.Founder.FounderAppServiceTests"
```

## Architecture Rules

### SDK Access Pattern (CRITICAL)

**Never call SDK-layer services directly from ViewModels or UI code.** All SDK functionality goes through app-layer service facades:

- `IProjectAppService` - project browsing, fetching, creation
- `IFounderAppService` - founder operations
- `IInvestmentAppService` - investing, withdrawing, recovery

ViewModels may inject these app services, `UIServices`, `INavigator`, `IWalletContext`.
ViewModels must **never** inject `IProjectService`, `IRelayService`, or other SDK-internal services.

### MediatR Operation Pattern

New SDK operations follow this structure in `Angor.Sdk/Funding/{area}/Operations/`:

```csharp
public static class OperationName
{
    public record OperationNameRequest(/* params */) : IRequest<Result<OperationNameResponse>>;
    public record OperationNameResponse(/* return data */);

    public class OperationNameHandler(/* deps via primary constructor */)
        : IRequestHandler<OperationNameRequest, Result<OperationNameResponse>>
    {
        public async Task<Result<OperationNameResponse>> Handle(
            OperationNameRequest request, CancellationToken cancellationToken)
        {
            // Implementation - always return Result<T>
        }
    }
}
```

App services delegate to MediatR: `mediator.Send(request)`.

### Result Type

Use `CSharpFunctionalExtensions.Result<T>` for all fallible operations. Never throw exceptions for expected failures.

```csharp
return Result.Success(new MyResponse(data));
return Result.Failure<MyResponse>("Error description");
return someResult.Map(x => new MyResponse(x));
return someResult.Bind(x => anotherOperation(x));
```

## Code Style

### Namespaces
- **Avalonia/SDK code**: file-scoped namespaces (`namespace Foo.Bar;`)
- **Legacy/test code**: block-scoped namespaces allowed (`namespace Foo.Bar { }`)
- New code should use file-scoped namespaces

### Formatting
- 4-space indentation, CRLF line endings
- Max line length: 120 characters
- Braces on new lines for all constructs (Allman style)
- Expression-bodied members: use for properties, indexers, accessors, lambdas; avoid for methods and constructors

### Naming Conventions
- Interfaces: `I` prefix, PascalCase (`IProjectAppService`)
- Types, properties, methods: PascalCase
- Private fields in ViewModels/Model: `camelCase` without underscore (`private readonly IMediator mediator;`)
- Private fields in test classes: `_camelCase` with underscore (`private readonly Mock<IProjectService> _mockProjectService;`)
- `[Reactive]` fields: `private` camelCase, generates PascalCase property (`[Reactive] private bool isDarkThemeEnabled;`)
- Strong domain types: `WalletId`, `ProjectId`, `Amount`, `TxId`, `Address`, `DomainFeeRate` (not raw strings/longs)

### Type Preferences
- Prefer explicit types over `var` (editorconfig setting, but not strictly enforced)
- Use `readonly` for fields that don't change after construction
- Primary constructors preferred for Handler classes and simple DI

### Imports
- `global using` statements in `GlobalUsings.cs` for common namespaces (ReactiveUI, System.Reactive, CSharpFunctionalExtensions)
- No specific import ordering enforced; follow existing file conventions

## ReactiveUI Patterns (Avalonia)

```csharp
public partial class MyViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    [Reactive] private string? name;  // generates public Name property

    public MyViewModel()
    {
        MyCommand = ReactiveCommand.Create(() => { /* ... */ }).DisposeWith(disposables);

        this.WhenAnyValue(x => x.Name)
            .Where(n => n != null)
            .Subscribe(DoSomething)
            .DisposeWith(disposables);
    }

    public void Dispose() => disposables.Dispose();
}
```

- Always dispose subscriptions via `CompositeDisposable` and `.DisposeWith(disposables)`
- Use `RxApp.MainThreadScheduler` when pushing values that trigger UI updates from background threads
- Use `EnhancedCommand` (from Zafiro) for commands with built-in `IsExecuting` and `Successes()`
- AXAML bindings to observables use the `^` operator: `{Binding Status^}`

## Test Conventions

- Test naming: `MethodName_WhenCondition_ExpectedResult` (SDK tests) or descriptive (`AddStage_adds_new_stage_to_project`)
- Arrange/Act/Assert pattern with comments
- Use `FluentAssertions`: `result.IsFailure.Should().BeTrue()`
- Use `Mock<T>` from Moq for dependencies
- Use `IClassFixture<T>` for shared test setup (e.g., network configuration)

## Dependency Injection

- Manual `ServiceCollection` built in `CompositionRoot.CreateMainViewModel()`
- Modular registration via extension methods: `AddModelServices()`, `AddViewModels()`, `AddUIServices()`
- MediatR registered with `services.AddMediatR(cfg => { ... })`
- Factory delegates for parameterized creation: `Func<IProject, IDetailsViewModel>`
- `ActivatorUtilities.CreateInstance` for ViewModels needing both DI services and runtime parameters
- Section ViewModels discovered via `[Section]`/`[SectionGroup]` attributes

## Key Libraries

| Library | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.3.12 | Desktop/mobile UI framework |
| ReactiveUI | 20.1.63 | MVVM with reactive extensions |
| MediatR | 12.5.0 | CQRS mediator pattern |
| CSharpFunctionalExtensions | 3.6.0 | Result type, Maybe, railway-oriented programming |
| NBitcoin | 7.0.46 | Bitcoin protocol operations |
| Zafiro | 46-51.x | UI toolkit, commands, dialogs |
| LiteDB | 5.0.21 | Local document storage |
| Serilog | 4.3.0 | Structured logging |
| xUnit | 2.9.2 | Test framework |
| FluentAssertions | 8.0.0 | Test assertions |

## CI/CD

- **`ci.yml`**: Build + test on push/PR to main (SDK tests + legacy tests)
- **`release-avalonia.yml`**: Triggered by `v*` tag push; builds Windows/Linux/macOS/Android installers, creates GitHub Release
- **`gh-deploy.yml`**: Triggered by `v*` tag push; deploys Blazor WASM to angor.io (gh-pages)
- **`gh-deploy-test.yml`**: Triggered by push to main; deploys to test.angor.io
- **`pr-deploy.yml`**: Manual workflow; deploys a PR to debug.angor.io

## Common Pitfalls

- **Thread affinity**: Pushing to `BehaviorSubject` from background threads will crash Avalonia. Use `RxApp.MainThreadScheduler.Schedule(() => subject.OnNext(value))`.
- **Indexer lag**: After broadcasting Bitcoin transactions, the indexer API may not reflect the change immediately. Use optimistic local updates and guard against stale responses in refresh handlers.
- **Two .NET versions**: Avalonia projects target net9.0, Blazor/Shared target net8.0. Don't mix SDK references.

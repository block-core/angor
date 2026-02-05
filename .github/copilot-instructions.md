# Angor Project - Copilot Instructions

## Architecture Guidelines

### SDK Access Pattern

**Never call SDK layer services directly from ViewModels or UI components.**

All SDK functionality must be accessed through one of the following app-layer services:

- `IProjectAppService` - For project-related operations (browsing, fetching, creating projects)
- `IFounderAppService` - For founder-related operations (managing founded projects)
- `IInvestmentAppService` - For investment-related operations (investing, withdrawing)

### Mediator Pattern

When adding new functionality to the SDK:

1. Create a new operation class in the appropriate `Operations` folder (e.g., `Angor.Sdk/Funding/Projects/Operations/`)
2. Follow the Request/Response/Handler pattern:
   ```csharp
   public static class OperationName
   {
       public record OperationNameRequest(/* parameters */) : IRequest<Result<OperationNameResponse>>;
       public record OperationNameResponse(/* return data */);
       
       public class OperationNameHandler(/* dependencies */)
           : IRequestHandler<OperationNameRequest, Result<OperationNameResponse>>
       {
           public async Task<Result<OperationNameResponse>> Handle(OperationNameRequest request, CancellationToken cancellationToken)
           {
               // Implementation
           }
       }
   }
   ```
3. Add the method to the appropriate app service interface (e.g., `IProjectAppService`)
4. Implement the method in the app service by calling `mediator.Send()`

### ViewModel Dependencies

ViewModels should inject:
- ✅ `IProjectAppService`, `IFounderAppService`, `IInvestmentAppService`
- ✅ `UIServices`, `INavigator`, `INetworkStorage`
- ❌ Never inject SDK-layer services like `IProjectService`, `IRelayService` directly

### Project Structure

```
Angor.Sdk/
├── Funding/
│   ├── Founder/
│   │   ├── IFounderAppService.cs      # App-layer interface
│   │   ├── FounderAppService.cs       # Uses IMediator
│   │   └── Operations/                # Request/Response/Handler classes
│   ├── Investment/
│   │   ├── IInvestmentAppService.cs
│   │   ├── InvestmentAppService.cs
│   │   └── Operations/
│   ├── Projects/
│   │   ├── IProjectAppService.cs
│   │   ├── ProjectAppService.cs
│   │   └── Operations/
│   └── Services/                      # SDK-layer services (internal use only)
│       ├── IProjectService.cs
│       ├── DocumentProjectService.cs
│       └── ...
```

### Result Type

Use `CSharpFunctionalExtensions.Result<T>` for all operations that can fail. This enables functional error handling throughout the codebase.

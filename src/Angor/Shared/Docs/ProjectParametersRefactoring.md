# Project Parameters Refactoring

## Overview

The `CreateInvestmentTransaction` method has been refactored to use a dedicated `ProjectParameters` class instead of multiple individual parameters. This improves code maintainability, extensibility, and makes the API more intuitive to use.

## Changes Made

### 1. New File: `ProjectParameters.cs`

Created a new parameter class to encapsulate all investment transaction parameters:

```csharp
public class ProjectParameters
{
    public string InvestorKey { get; set; }
    public long TotalInvestmentAmount { get; set; }
  public DateTime? InvestmentStartDate { get; set; }
    public byte PatternIndex { get; set; }
}
```

#### Helper Methods

**`Create()`** - For simple/legacy scenarios:
```csharp
public static ProjectParameters Create(string investorKey, long totalInvestmentAmount)
{
    return new ProjectParameters
    {
        InvestorKey = investorKey,
  TotalInvestmentAmount = totalInvestmentAmount,
    InvestmentStartDate = DateTime.UtcNow,
        PatternIndex = 0
    };
}
```

**`CreateForDynamicProject()`** - For Fund/Subscribe projects with pattern selection:
```csharp
public static ProjectParameters CreateForDynamicProject(
    string investorKey, 
    long totalInvestmentAmount, 
    byte patternIndex,
    DateTime? investmentStartDate = null)
{
    return new ProjectParameters
    {
        InvestorKey = investorKey,
   TotalInvestmentAmount = totalInvestmentAmount,
 InvestmentStartDate = investmentStartDate ?? DateTime.UtcNow,
        PatternIndex = patternIndex
    };
}
```

### 2. Updated: `IInvestorTransactionActions.cs`

Added new method overload while keeping the legacy signature for backward compatibility:

```csharp
/// <summary>
/// Creates an investment transaction using the legacy method signature (backward compatible).
/// </summary>
Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, long totalInvestmentAmount);

/// <summary>
/// Creates an investment transaction using project parameters.
/// Recommended for new code as it supports pattern selection and explicit start dates.
/// </summary>
Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, ProjectParameters parameters);
```

### 3. Updated: `InvestorTransactionActions.cs`

**Legacy Method** - Now delegates to the new parameter-based method:
```csharp
public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, long totalInvestmentAmount)
{
    // Legacy method - delegates to new parameter-based method with defaults
    return CreateInvestmentTransaction(projectInfo, ProjectParameters.Create(investorKey, totalInvestmentAmount));
}
```

**New Method** - Cleaner signature using the parameter class:
```csharp
public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, ProjectParameters parameters)
{
    var investmentStartDate = parameters.InvestmentStartDate ?? DateTime.UtcNow;

    var opreturnScript = _projectScriptsBuilder.BuildInvestorInfoScript(
        parameters.InvestorKey, 
        projectInfo, 
        investmentStartDate, 
        parameters.PatternIndex);

    var expiryDateOverride = GetExpiryDateOverride(projectInfo, parameters.TotalInvestmentAmount);

    // ... build stage scripts using parameters.InvestorKey, parameters.PatternIndex, etc.

    return _investmentTransactionBuilder.BuildInvestmentTransaction(
        projectInfo, opreturnScript, stagesScript, parameters.TotalInvestmentAmount);
}
```

## Benefits

### 1. **Cleaner Method Signatures**
- **Before**: `CreateInvestmentTransaction(projectInfo, investorKey, totalInvestmentAmount)` + need to add more params
- **After**: `CreateInvestmentTransaction(projectInfo, parameters)` - clean and extensible

### 2. **Easier to Extend**
- Adding new parameters only requires updating `ProjectParameters` class
- No need to change method signatures throughout the codebase
- Existing code continues to work

### 3. **Better Discoverability**
- All related parameters grouped in one class
- IntelliSense shows all available options
- Factory methods provide guided construction

### 4. **Type Safety**
- Named properties instead of positional parameters
- Harder to pass parameters in wrong order
- Clear intent in code

### 5. **Backward Compatible**
- Existing code using legacy method continues to work
- No breaking changes
- Gradual migration path

## Usage Examples

### Legacy Usage (Still Supported)

```csharp
// Old way - still works
var transaction = investorActions.CreateInvestmentTransaction(
  projectInfo, 
    investorKey, 
    100000000);
```

### New Usage - Simple Investment

```csharp
// Using factory method for simple case
var parameters = ProjectParameters.Create(investorKey, 100000000);

var transaction = investorActions.CreateInvestmentTransaction(projectInfo, parameters);
```

### New Usage - Dynamic Project with Pattern Selection

```csharp
// Fund/Subscribe project with custom pattern and start date
var parameters = ProjectParameters.CreateForDynamicProject(
    investorKey: "03abc...",
    totalInvestmentAmount: 100000000,
    patternIndex: 1, // Select the 6-month plan
    investmentStartDate: new DateTime(2025, 1, 15));

var transaction = investorActions.CreateInvestmentTransaction(projectInfo, parameters);
```

### New Usage - Full Control

```csharp
// Direct construction for maximum control
var parameters = new ProjectParameters
{
    InvestorKey = "03abc...",
    TotalInvestmentAmount = 100000000,
    PatternIndex = 2, // 12-month plan
    InvestmentStartDate = DateTime.UtcNow
};

var transaction = investorActions.CreateInvestmentTransaction(projectInfo, parameters);
```

## UI Integration Example

### Pattern Selection UI

```csharp
public class InvestViewModel
{
    public void CreateInvestment(string selectedPatternId)
    {
        // Find the pattern index by ID
    var patternIndex = (byte)_projectInfo.DynamicStagePatterns
            .FindIndex(p => p.PatternId == selectedPatternId);

        var parameters = ProjectParameters.CreateForDynamicProject(
  investorKey: _walletService.GetInvestorKey(),
            totalInvestmentAmount: InvestmentAmount,
       patternIndex: patternIndex);

        var transaction = _investorActions.CreateInvestmentTransaction(
   _projectInfo, 
   parameters);

        // Broadcast transaction...
    }
}
```

## Migration Guide

### For Existing Code

**No action required!** The legacy method signature is still available and works exactly as before.

### For New Code

**Recommended**: Use the new parameter-based method:

```csharp
// Instead of this:
var tx = investorActions.CreateInvestmentTransaction(projectInfo, key, amount);

// Use this:
var params = ProjectParameters.Create(key, amount);
var tx = investorActions.CreateInvestmentTransaction(projectInfo, params);
```

### For Dynamic Projects

**Required**: Use the new method with pattern selection:

```csharp
var parameters = ProjectParameters.CreateForDynamicProject(
    investorKey,
    amount,
    selectedPatternIndex);

var tx = investorActions.CreateInvestmentTransaction(projectInfo, parameters);
```

## Future Enhancements

The `ProjectParameters` class makes it easy to add new features:

### 1. Custom Fee Selection
```csharp
public class ProjectParameters
{
    // ...existing properties...
    public long? CustomFeeRate { get; set; }
}
```

### 2. Change Address
```csharp
public class ProjectParameters
{
    // ...existing properties...
    public string ChangeAddress { get; set; }
}
```

### 3. UTXO Selection
```csharp
public class ProjectParameters
{
    // ...existing properties...
  public List<OutPoint> SelectedUtxos { get; set; }
}
```

### 4. Metadata
```csharp
public class ProjectParameters
{
  // ...existing properties...
    public Dictionary<string, string> Metadata { get; set; }
}
```

## Testing

The new parameter class makes testing easier:

```csharp
[Fact]
public void CreateInvestment_WithCustomPattern_UsesCorrectPattern()
{
    // Arrange
    var parameters = new ProjectParameters
    {
        InvestorKey = TestKeys.InvestorKey,
        TotalInvestmentAmount = 100000000,
  PatternIndex = 2, // Test specific pattern
        InvestmentStartDate = new DateTime(2025, 1, 15)
    };

    // Act
    var transaction = _sut.CreateInvestmentTransaction(_projectInfo, parameters);

    // Assert
    // Verify transaction uses pattern 2...
}
```

## Files Modified

- ? Created: `..\Shared\Models\ProjectParameters.cs` (renamed from InvestmentParameters.cs)
- ? Modified: `..\Shared\Protocol\IInvestorTransactionActions.cs`
- ? Modified: `..\Shared\Protocol\InvestorTransactionActions.cs`

## Build Status

? Build successful - All projects compile without errors

## Summary

The refactoring successfully introduces a cleaner, more maintainable API for creating investment transactions while maintaining 100% backward compatibility. The new `ProjectParameters` class makes the code easier to understand, extend, and test, and provides a better developer experience for users of the API.

Key improvements:
- ? Cleaner method signatures
- ? Easier to extend with new parameters
- ? Better type safety and discoverability
- ? Backward compatible - no breaking changes
- ? Ready for UI integration
- ? Supports dynamic project pattern selection

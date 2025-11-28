# Dynamic Stages - OP_RETURN Script Encoding

## Overview

This implementation adds support for encoding dynamic stage information in OP_RETURN scripts for Fund and Subscribe project types. The information is compactly encoded in just 7 bytes, allowing it to fit efficiently in Bitcoin transactions.

## Changes Made

### 1. New Files Created

#### `DynamicStageInfo.cs`
- Compact 7-byte encoding of dynamic stage information
- Format: `[2 bytes: start days since epoch] [1 byte: pattern ID] [1 byte: stage count] [1 byte: frequency] [1 byte: payout type] [1 byte: payout day]`
- Uses epoch date (Jan 1, 2025) for compact date storage
- Supports encoding/decoding to/from byte arrays
- Helper methods to convert between `DynamicStagePattern` and encoded format

### 2. Modified Files

#### `IProjectScriptsBuilder.cs` & `ProjectScriptsBuilder.cs`
**Updated Methods:**
- `BuildInvestorInfoScript(string investorKey, ProjectInfo projectInfo, DateTime? investmentStartDate = null)`
  - For **Invest** projects: Creates OP_RETURN with investor key only (2 ops)
  - For **Fund/Subscribe** projects: Creates OP_RETURN with investor key + 7-byte dynamic info (3 ops)

- `BuildSeederInfoScript(string investorKey, uint256 secretHash, ProjectInfo projectInfo, DateTime? investmentStartDate = null)`
  - For **Invest** projects: Creates OP_RETURN with investor key + secret hash (3 ops)
  - For **Fund/Subscribe** projects: Creates OP_RETURN with investor key + secret hash + 7-byte dynamic info (4 ops)

**New Method:**
- `GetDynamicStageInfoFromOpReturnScript(Script script)` - Extracts dynamic stage info from OP_RETURN if present

#### `IInvestmentScriptBuilder.cs` & `InvestmentScriptBuilder.cs`
**New Overload:**
- `BuildProjectScriptsForStage(..., DateTime? investmentStartDate)` - Added parameter for dynamic stage calculation

**Updated Logic:**
- For **Invest** projects: Uses predefined `projectInfo.Stages[stageIndex].ReleaseDate`
- For **Fund/Subscribe** projects: Calculates release date dynamically using:
  - Investment start date
  - Dynamic stage pattern
  - Stage index
- Implements three payout calculation methods:
  1. `FromStartDate`: Fixed intervals from investment date
  2. `SpecificDayOfMonth`: Specific day each month (handles months with fewer days)
  3. `SpecificDayOfWeek`: Specific weekday (e.g., every Monday)

#### `SeederTransactionActions.cs`
**Updated `CreateInvestmentTransaction`:**
- Captures investment start date (`DateTime.UtcNow`)
- Passes `projectInfo` and `investmentStartDate` to `BuildSeederInfoScript`
- For dynamic projects: Uses pattern's `StageCount` instead of `projectInfo.Stages.Count`
- Passes `investmentStartDate` when building scripts for dynamic projects

#### `InvestorTransactionActions.cs`
**Updated `CreateInvestmentTransaction`:**
- Captures investment start date (`DateTime.UtcNow`)
- Passes `projectInfo` and `investmentStartDate` to `BuildInvestorInfoScript`
- For dynamic projects: Uses pattern's `StageCount` instead of `projectInfo.Stages.Count`
- Passes `investmentStartDate` when building scripts for dynamic projects

### 3. OP_RETURN Script Formats

#### Invest Project (Investor)
```
OP_RETURN <investor_pubkey>
Total: 2 ops
```

#### Invest Project (Seeder)
```
OP_RETURN <investor_pubkey> <secret_hash>
Total: 3 ops (secret hash = 32 bytes)
```

#### Fund/Subscribe Project (Investor)
```
OP_RETURN <investor_pubkey> <dynamic_info>
Total: 3 ops (dynamic info = 7 bytes)
```

#### Fund/Subscribe Project (Seeder)
```
OP_RETURN <investor_pubkey> <secret_hash> <dynamic_info>
Total: 4 ops (secret hash = 32 bytes, dynamic info = 7 bytes)
```

## Dynamic Info Encoding (7 bytes)

| Bytes | Field | Description |
|-------|-------|-------------|
| 0-1 | Investment Start Days | Days since epoch (Jan 1, 2025) - little endian |
| 2 | Pattern ID | Index of pattern in project (0-255) |
| 3 | Stage Count | Number of stages (1-255) |
| 4 | Frequency | StageFrequency enum value |
| 5 | Payout Day Type | PayoutDayType enum value |
| 6 | Payout Day | Day value (depends on payout type) |

## Usage Example

### Creating an Investment Transaction (Fund Project)

```csharp
// Founder creates a Fund project with monthly pattern
var projectInfo = new ProjectInfo
{
    ProjectType = ProjectType.Fund,
    DynamicStagePatterns = new List<DynamicStagePattern>
    {
        new DynamicStagePattern
   {
   Frequency = StageFrequency.Monthly,
     StageCount = 6,
     PayoutDayType = PayoutDayType.FromStartDate
      }
    }
};

// Investor makes investment
var investorKey = "...";
var investmentStartDate = DateTime.UtcNow;

// Create OP_RETURN script with dynamic info embedded
var opReturnScript = projectScriptsBuilder.BuildInvestorInfoScript(
    investorKey, 
    projectInfo, 
    investmentStartDate);

// Build stage scripts with calculated release dates
for (int i = 0; i < projectInfo.DynamicStagePatterns[0].StageCount; i++)
{
    var stageScripts = investmentScriptBuilder.BuildProjectScriptsForStage(
      projectInfo,
        investorKey,
   i,
        hashOfSecret: null,
        expiryDateOverride: null,
        investmentStartDate: investmentStartDate);
    
    // stageScripts.Founder contains timelock for calculated release date
}
```

### Decoding Dynamic Info from Transaction

```csharp
// Parse investment transaction
var transaction = network.CreateTransaction(txHex);
var opReturnScript = transaction.Outputs
    .First(_ => _.ScriptPubKey.IsUnspendable)
    .ScriptPubKey;

// Extract dynamic info if present
var dynamicInfo = projectScriptsBuilder.GetDynamicStageInfoFromOpReturnScript(opReturnScript);

if (dynamicInfo != null)
{
    // This is a dynamic project
    var investmentDate = dynamicInfo.GetInvestmentStartDate();
    var pattern = dynamicInfo.ToPattern();
    
    // Calculate stage dates
 for (int i = 0; i < dynamicInfo.StageCount; i++)
    {
       var releaseDate = CalculateDynamicStageReleaseDate(
  investmentDate, 
     pattern, 
            i);
    }
}
```

## Benefits

1. **Compact Storage**: Only 7 bytes needed for all dynamic stage information
2. **Self-Contained**: Each investment transaction contains all info needed to calculate stages
3. **Flexible**: Supports multiple payout patterns and frequencies
4. **Backward Compatible**: Invest projects continue to work unchanged
5. **Efficient**: Uses epoch-based date encoding to minimize space

## Stage Release Date Calculation

### From Start Date Pattern
```
Stage 0: investmentDate + (30 days * 1) = Day 30
Stage 1: investmentDate + (30 days * 2) = Day 60
Stage 2: investmentDate + (30 days * 3) = Day 90
```

### Fixed Day of Month (e.g., 15th)
```
Investment: Jan 10
Stage 0: Jan 15 (first occurrence)
Stage 1: Feb 15 (one month later)
Stage 2: Mar 15 (one month later)
```

### Fixed Day of Week (e.g., Monday)
```
Investment: Thursday Jan 11
Stage 0: Monday Jan 15 (next Monday)
Stage 1: Monday Jan 22 (next week)
Stage 2: Monday Jan 29 (next week)
```

## Testing

All existing tests pass. Key test updates:
- `SeederTransactionActionsTest`: Updated to pass `projectInfo` and accept any `DateTime?`
- Tests now properly handle both Invest (fixed) and Fund/Subscribe (dynamic) project types

## Next Steps

1. **UI Integration**: Add UI to select/configure dynamic patterns
2. **Storage**: Store investment start dates with investment records
3. **Display**: Show calculated stage dates in investor views
4. **Validation**: Add validation for dynamic pattern configurations
5. **Testing**: Add specific tests for dynamic stage date calculation

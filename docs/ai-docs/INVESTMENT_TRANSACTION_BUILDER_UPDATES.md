# InvestmentTransactionBuilder Updates for Dynamic Stages

## Overview
Updated `InvestmentTransactionBuilder` to support dynamic stage generation for Fund and Subscribe project types while maintaining backward compatibility with fixed-stage Invest projects.

## Key Changes

### 1. **Dynamic Stage Count Detection**
The builder now intelligently determines stage count based on project type:

**For Invest Projects (Fixed Stages):**
- Uses `projectInfo.Stages.Count` from the predefined stages
- Validates that stage count matches the provided `projectScripts` count
- Uses predefined `AmountToRelease` percentages from each stage

**For Fund/Subscribe Projects (Dynamic Stages):**
- Uses `projectScripts.Count()` (determined dynamically by caller)
- Equal distribution: `100% / stageCount` for each stage
- Stage count and dates are calculated from `DynamicStagePattern` when investment is created

### 2. **Updated Method: `BuildInvestmentTransaction`**

**Before:**
```csharp
// Always used projectInfo.Stages.Count
for (int i = 0; i < projectInfo.Stages.Count; i++)
{
    stageAmount = Convert.ToInt64(totalInvestmentAmountAfterFee * (projectInfo.Stages[i].AmountToRelease / 100));
}
```

**After:**
```csharp
// Dynamically determines stage count and percentages
int stageCount = projectScripts.Count();

if (projectInfo.AllowDynamicStages)
{
    // Equal distribution for Fund/Subscribe
    decimal equalPercentage = 100m / stageCount;
    stagePercentages.AddRange(Enumerable.Repeat(equalPercentage, stageCount));
}
else
{
    // Use predefined percentages for Invest
  stagePercentages.AddRange(projectInfo.Stages.Select(s => s.AmountToRelease));
}
```

### 3. **Updated Methods: Recovery Transaction Builders**

Both `BuildUpfrontRecoverFundsTransaction` and `BuildUpfrontUnfundedReleaseFundsTransaction` now handle dynamic stages:

```csharp
// Determine stage count based on project type
int stageCount;
if (projectInfo.AllowDynamicStages)
{
    // For dynamic stages, count outputs after fee and OP_RETURN (outputs 2+)
    stageCount = investmentTransaction.Outputs.Count - 2;
}
else
{
    // For fixed stages, use Stages.Count
  stageCount = projectInfo.Stages.Count;
}
```

## How It Works

### For Invest Projects (Fixed Stages)
1. Caller passes predefined `ProjectScripts` (one per stage in `projectInfo.Stages`)
2. Builder validates stage count matches
3. Uses `projectInfo.Stages[i].AmountToRelease` for each stage
4. Creates outputs with predefined percentages

### For Fund/Subscribe Projects (Dynamic Stages)
1. Caller generates `ProjectScripts` based on selected pattern
 - Pattern determines: frequency, stage count, payout rules
   - See `InvestorTransactionActions.CreateInvestmentTransaction` for pattern selection
2. Builder receives dynamic `projectScripts` collection
3. Calculates equal distribution: `100% / stageCount`
4. Creates outputs with equal percentages

## Example Usage

### Invest Project (Fixed Stages)
```csharp
// Project has 3 predefined stages: 30%, 40%, 30%
var projectInfo = new ProjectInfo 
{
    ProjectType = ProjectType.Invest,
    Stages = new List<Stage>
    {
        new Stage { AmountToRelease = 30, ReleaseDate = DateTime.UtcNow.AddMonths(3) },
        new Stage { AmountToRelease = 40, ReleaseDate = DateTime.UtcNow.AddMonths(6) },
        new Stage { AmountToRelease = 30, ReleaseDate = DateTime.UtcNow.AddMonths(9) }
    }
};

// Caller creates 3 ProjectScripts (matches Stages.Count)
var projectScripts = Enumerable.Range(0, 3)
    .Select(i => investmentScriptBuilder.BuildProjectScriptsForStage(...));

// Builder uses projectInfo.Stages[i].AmountToRelease
var transaction = builder.BuildInvestmentTransaction(projectInfo, opReturn, projectScripts, amount);
// Result: 3 outputs with 30%, 40%, 30% distribution
```

### Fund/Subscribe Project (Dynamic Stages)
```csharp
// Project has dynamic pattern: 6-month subscription with monthly stages
var projectInfo = new ProjectInfo 
{
  ProjectType = ProjectType.Fund,
    DynamicStagePatterns = new List<DynamicStagePattern>
    {
        new DynamicStagePattern 
        { 
            PatternId = 0,
            StageCount = 6,
      Frequency = StageFrequency.Monthly,
      Name = "6-Month Plan"
        }
    }
};

// Caller creates 6 ProjectScripts based on pattern
var pattern = projectInfo.DynamicStagePatterns[patternIndex];
var investmentStartDate = DateTime.UtcNow;

var projectScripts = Enumerable.Range(0, pattern.StageCount)
    .Select(i => investmentScriptBuilder.BuildProjectScriptsForStage(
        projectInfo, investorKey, i, null, null, investmentStartDate, patternIndex));

// Builder calculates equal distribution: 100% / 6 = 16.666...%
var transaction = builder.BuildInvestmentTransaction(projectInfo, opReturn, projectScripts, amount);
// Result: 6 outputs with ~16.67% each (last stage gets remainder)
```

## Backward Compatibility

? **Fully Backward Compatible**
- Existing Invest projects continue working unchanged
- Uses `AllowDynamicStages` property to determine behavior
- No breaking changes to method signatures
- All existing tests pass

## Validation

The builder includes validation to ensure consistency:

```csharp
if (!projectInfo.AllowDynamicStages && projectInfo.Stages.Count != stageCount)
{
    throw new InvalidOperationException(
 $"Stage count mismatch: expected {stageCount} stages, but ProjectInfo has {projectInfo.Stages?.Count ?? 0}");
}
```

## Transaction Structure

Both project types create the same transaction structure:

**Output 0:** Angor Fee (1% of total investment)  
**Output 1:** OP_RETURN (investor info + dynamic stage info if applicable)  
**Outputs 2+:** Stage outputs (Taproot scripts)

The key difference:
- **Invest**: Stage percentages from `projectInfo.Stages[i].AmountToRelease`
- **Fund/Subscribe**: Equal percentage per stage (100% / stageCount)

## Integration Points

This change integrates with:

1. **InvestorTransactionActions.CreateInvestmentTransaction**
   - Generates `projectScripts` based on project type
   - Passes correct parameters to builder

2. **IInvestmentScriptBuilder.BuildProjectScriptsForStage**
   - Now accepts `investmentStartDate` and `patternIndex`
   - Calculates dynamic stage dates when needed

3. **IProjectScriptsBuilder.BuildInvestorInfoScript**
   - Encodes dynamic stage info in OP_RETURN for Fund/Subscribe

## Future Enhancements

Possible future improvements:
- Support custom percentage distributions for Fund/Subscribe projects
- Allow founders to define multiple patterns with different distributions
- Support pattern switching (e.g., upgrade from 3-month to 6-month)

## Testing Recommendations

Test scenarios:
1. ? Invest project with 3 fixed stages (30%, 40%, 30%)
2. ? Fund project with 6 monthly stages (equal distribution)
3. ? Subscribe project with 12 weekly stages (equal distribution)
4. ? Recovery transactions for all project types
5. ? Stage count validation for Invest projects
6. ? Last stage gets remainder calculation

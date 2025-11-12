# Pattern Index Selection for Dynamic Stages

## Overview

Fund and Subscribe projects can offer multiple dynamic stage patterns (e.g., 3-month, 6-month, 12-month subscriptions). The `patternIndex` parameter allows investors to choose which pattern they want to use when making an investment.

## Pattern Index Parameter

### Type
- **`byte`** (0-255)
- Represents the index in `ProjectInfo.DynamicStagePatterns` list

### Default Value
- **`0`** (first pattern)
- Used when not specified

### Usage Locations

The `patternIndex` parameter is now consistently used across three key methods:

1. **`BuildInvestorInfoScript`** - Encodes pattern choice in OP_RETURN
2. **`BuildSeederInfoScript`** - Encodes pattern choice in OP_RETURN  
3. **`BuildProjectScriptsForStage`** - Uses pattern to calculate stage dates

## Why Pattern Index?

### Benefits
1. **Consistency**: Same byte value (0-255) used everywhere
2. **Compact**: Only 1 byte in OP_RETURN encoding (byte 2)
3. **Validation**: Can validate index is within range
4. **Future-proof**: Supports up to 256 different patterns per project

### Alternative Considered
Using `PatternId` (string) was considered but rejected because:
- Requires string matching/lookup
- Less efficient for encoding
- More error-prone
- Unnecessary complexity

## Example: Multi-Pattern Project

```csharp
var projectInfo = new ProjectInfo
{
    ProjectType = ProjectType.Fund,
    DynamicStagePatterns = new List<DynamicStagePattern>
    {
    // Pattern 0: 3-month subscription
        new DynamicStagePattern
        {
            PatternId = "3-month",
   Name = "3-Month Plan",
   Frequency = StageFrequency.Monthly,
   StageCount = 3
     },
      // Pattern 1: 6-month subscription
        new DynamicStagePattern
     {
    PatternId = "6-month",
   Name = "6-Month Plan",
        Frequency = StageFrequency.Monthly,
 StageCount = 6
        },
        // Pattern 2: 12-month subscription
        new DynamicStagePattern
    {
   PatternId = "12-month",
   Name = "12-Month Plan",
    Frequency = StageFrequency.Monthly,
  StageCount = 12
   }
    }
};
```

## Investor Pattern Selection

### Current Implementation (Default Pattern 0)

```csharp
// Default to pattern 0
byte patternIndex = 0;

var opReturnScript = projectScriptsBuilder.BuildInvestorInfoScript(
    investorKey, 
    projectInfo, 
    DateTime.UtcNow, 
    patternIndex);
```

### Future Enhancement (User Selection)

```csharp
// UI allows user to select pattern
public Transaction CreateInvestmentWithPattern(
    ProjectInfo projectInfo, 
    string investorKey,
    long amount,
  byte selectedPatternIndex)
{
    var investmentStartDate = DateTime.UtcNow;
    
  // Validate pattern selection
    if (selectedPatternIndex >= projectInfo.DynamicStagePatterns.Count)
  {
   throw new ArgumentException($"Invalid pattern index {selectedPatternIndex}");
    }
    
    // Use selected pattern
    var opReturnScript = _projectScriptsBuilder.BuildInvestorInfoScript(
        investorKey, 
        projectInfo, 
        investmentStartDate, 
  selectedPatternIndex);
    
    var pattern = projectInfo.DynamicStagePatterns[selectedPatternIndex];
    var stagesScript = Enumerable.Range(0, pattern.StageCount)
        .Select(i => _investmentScriptBuilder.BuildProjectScriptsForStage(
            projectInfo, investorKey, i, null, null, investmentStartDate, selectedPatternIndex))
  .ToList();
    
    return _investmentTransactionBuilder.BuildInvestmentTransaction(
        projectInfo, opReturnScript, stagesScript, amount);
}
```

## Decoding Pattern from Transaction

```csharp
// Extract dynamic info from OP_RETURN
var dynamicInfo = projectScriptsBuilder.GetDynamicStageInfoFromOpReturnScript(opReturnScript);

if (dynamicInfo != null)
{
    byte usedPatternIndex = dynamicInfo.PatternId;
    
    // Look up the pattern that was used
    if (usedPatternIndex < projectInfo.DynamicStagePatterns.Count)
    {
  var pattern = projectInfo.DynamicStagePatterns[usedPatternIndex];
        Console.WriteLine($"Investor chose: {pattern.Name}");
        Console.WriteLine($"Stages: {pattern.StageCount}");
     Console.WriteLine($"Frequency: {pattern.Frequency}");
    }
}
```

## Validation

All three methods now validate the pattern index:

```csharp
// Validate pattern index
if (patternIndex >= projectInfo.DynamicStagePatterns.Count)
{
    throw new ArgumentOutOfRangeException(nameof(patternIndex), 
        $"Pattern index {patternIndex} is out of range. Project has {projectInfo.DynamicStagePatterns.Count} patterns.");
}
```

## OP_RETURN Encoding

The pattern index is stored in **byte 2** of the 7-byte dynamic info:

```
[0-1] Investment start days (ushort)
[2]   Pattern index (byte) ? HERE
[3]   Stage count (byte)
[4]   Frequency (byte)
[5]   Payout day type (byte)
[6]   Payout day (byte)
```

## UI Recommendations

### Pattern Selection UI

```csharp
// Display available patterns to user
foreach (var (pattern, index) in projectInfo.DynamicStagePatterns.Select((p, i) => (p, i)))
{
    DisplayPattern(pattern, (byte)index);
}

void DisplayPattern(DynamicStagePattern pattern, byte index)
{
  Console.WriteLine($"[{index}] {pattern.Name}");
    Console.WriteLine($"    {pattern.Description}");
    Console.WriteLine($"    {pattern.StageCount} stages, {pattern.Frequency}");
    
    // Show estimated stage dates
    var previewDates = CalculatePreviewDates(pattern, DateTime.UtcNow);
  foreach (var date in previewDates)
    {
        Console.WriteLine($"    - {date:MMM dd, yyyy}");
    }
}
```

## Migration Notes

### Existing Code
- **Before**: Pattern was hardcoded to index 0
- **After**: Pattern index can be specified (defaults to 0)
- **Impact**: Backward compatible - default behavior unchanged

### Next Steps for Full Pattern Selection

1. **Add UI**: Let users choose pattern when investing
2. **Update Services**: Pass selected pattern through service layers
3. **Validation**: Ensure selected pattern is valid
4. **Display**: Show which pattern was used for each investment
5. **Analytics**: Track which patterns are most popular

## Benefits Summary

? **Flexible**: Supports multiple subscription/funding options  
? **Efficient**: Only 1 byte overhead  
? **Validated**: Range-checked at multiple points  
? **Consistent**: Same index used throughout  
? **Future-proof**: Easy to extend to 256 patterns  
? **Backward compatible**: Defaults to pattern 0

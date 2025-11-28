# Dynamic Stage Calculator Refactoring

## Overview

The stage calculation methods have been extracted from `InvestmentScriptBuilder` into a dedicated `DynamicStageCalculator` class in the `Angor.Shared.Models` namespace. This improves code organization, reusability, and testability.

## Changes Made

### 1. New File: `DynamicStageCalculator.cs`

Created a new static class with three public methods:

#### `CalculateDynamicStageReleaseDate`
```csharp
public static DateTime CalculateDynamicStageReleaseDate(
    DateTime investmentStartDate, 
    DynamicStagePattern pattern, 
    int stageIndex)
```
- **Purpose**: Main entry point for calculating stage release dates
- **Logic**: Routes to appropriate calculation method based on `PayoutDayType`
- **Used by**: `InvestmentScriptBuilder`, `DynamicStageHelper`

#### `CalculateMonthlyPayoutDate`
```csharp
public static DateTime CalculateMonthlyPayoutDate(
    DateTime startDate, 
    StageFrequency frequency, 
    int dayOfMonth, 
    int stageIndex)
```
- **Purpose**: Calculates dates for specific day-of-month patterns
- **Supports**: Monthly, BiMonthly, Quarterly frequencies
- **Edge cases handled**:
  - Months with fewer days (e.g., Feb 31 ? Feb 28/29)
  - Leap years
  - Year boundaries
  - Start date after target day (moves to next month)

#### `CalculateWeeklyPayoutDate`
```csharp
public static DateTime CalculateWeeklyPayoutDate(
    DateTime startDate, 
    StageFrequency frequency, 
 int dayOfWeek, 
    int stageIndex)
```
- **Purpose**: Calculates dates for specific day-of-week patterns
- **Supports**: Weekly, Biweekly frequencies
- **Edge cases handled**:
  - Finding next occurrence of target day
  - Same-day start (uses that day)
  - Year boundaries
  - All days of week (Sunday=0 through Saturday=6)

### 2. Updated: `InvestmentScriptBuilder.cs`

The three private static methods are now simple wrappers:

```csharp
private static DateTime CalculateDynamicStageReleaseDate(...)
{
    return DynamicStageCalculator.CalculateDynamicStageReleaseDate(...);
}

private static DateTime CalculateNextMonthlyPayoutDate(...)
{
    return DynamicStageCalculator.CalculateMonthlyPayoutDate(...);
}

private static DateTime CalculateNextWeeklyPayoutDate(...)
{
 return DynamicStageCalculator.CalculateWeeklyPayoutDate(...);
}
```

**Note**: These wrappers maintain backward compatibility within `InvestmentScriptBuilder`. They could be removed in favor of direct calls to `DynamicStageCalculator`.

### 3. Updated: `DynamicStageHelper.ComputeStagesFromPattern`

Simplified to use the new calculator:

```csharp
public static List<Stage> ComputeStagesFromPattern(
    DynamicStagePattern pattern, 
    DateTime investmentStartDate)
{
    var stages = new List<Stage>();
    var percentagePerStage = 100m / pattern.StageCount;

    for (int i = 0; i < pattern.StageCount; i++)
    {
     var releaseDate = DynamicStageCalculator.CalculateDynamicStageReleaseDate(
   investmentStartDate, pattern, i);

        stages.Add(new Stage
{
         ReleaseDate = releaseDate,
            AmountToRelease = percentagePerStage
        });
    }

    return stages;
}
```

The duplicate private methods (`CalculateNextMonthlyPayoutDate` and `CalculateNextWeeklyPayoutDate`) have been removed from `DynamicStageHelper`.

### 4. New File: `DynamicStageCalculatorTests.cs`

Comprehensive test suite with **25 tests** covering:

#### `CalculateDynamicStageReleaseDate` Tests (4 tests)
- ? FromStartDate with Monthly frequency
- ? FromStartDate with Weekly frequency
- ? SpecificDayOfMonth pattern
- ? SpecificDayOfWeek pattern

#### `CalculateMonthlyPayoutDate` Tests (9 tests)
- ? First occurrence after start date
- ? Moves to next month if day passed
- ? Handles February (non-leap year)
- ? Handles leap year
- ? BiMonthly frequency
- ? Quarterly frequency
- ? Invalid frequency throws exception
- ? Months with 30 days (e.g., April)
- ? Year boundary crossing

#### `CalculateWeeklyPayoutDate` Tests (9 tests)
- ? Finds next occurrence of target day
- ? Multiple stages (weekly progression)
- ? Biweekly frequency
- ? Same day as start date
- ? Sunday (day 0)
- ? Saturday (day 6)
- ? Invalid day of week throws exception
- ? Invalid frequency throws exception
- ? Year boundary crossing

#### Integration Tests (3 tests)
- ? Monthly payout across year boundary
- ? Weekly payout across year boundary
- ? All frequencies produce distinct, increasing dates

## Test Results

```
Test summary: total: 25, failed: 0, succeeded: 25, skipped: 0
```

All tests pass successfully! ?

## Benefits

### 1. **Separation of Concerns**
- Stage calculation logic isolated from script building
- Easier to understand and maintain
- Single Responsibility Principle

### 2. **Reusability**
- Can be used anywhere in the codebase
- Currently used by:
  - `InvestmentScriptBuilder` (script generation)
  - `DynamicStageHelper` (stage preview/computation)
- Future uses could include:
  - UI date previews
  - Validation logic
  - Analytics/reporting

### 3. **Testability**
- Pure functions with no dependencies
- Easy to test edge cases
- Comprehensive test coverage
- Tests serve as documentation

### 4. **Maintainability**
- All date calculation logic in one place
- Changes only need to be made once
- Tests ensure changes don't break existing behavior

### 5. **Discoverability**
- Clear, descriptive class name
- Well-documented public API
- Easy to find and use

## Edge Cases Handled

### Monthly Payouts
- ? Months with different numbers of days (28, 29, 30, 31)
- ? Leap years vs non-leap years
- ? Requesting the 31st in months with fewer days
- ? Start date after target day in month
- ? Year boundaries (Dec ? Jan)

### Weekly Payouts
- ? All days of week (Sunday through Saturday)
- ? Start date on the target day
- ? Start date before target day
- ? Year boundaries
- ? Invalid day of week values (< 0 or > 6)

### General
- ? All frequency types
- ? Multiple stages in sequence
- ? Validation of inputs
- ? UTC timezone consistency

## Usage Examples

### Basic Usage

```csharp
var pattern = new DynamicStagePattern
{
    PayoutDayType = PayoutDayType.SpecificDayOfMonth,
    Frequency = StageFrequency.Monthly,
    PayoutDay = 15, // 15th of each month
    StageCount = 6
};

var investmentDate = DateTime.UtcNow;

for (int i = 0; i < pattern.StageCount; i++)
{
    var releaseDate = DynamicStageCalculator.CalculateDynamicStageReleaseDate(
        investmentDate, pattern, i);
    
    Console.WriteLine($"Stage {i}: {releaseDate:MMM dd, yyyy}");
}
```

### Direct Method Calls

```csharp
// Calculate specific monthly payout
var payoutDate = DynamicStageCalculator.CalculateMonthlyPayoutDate(
    startDate: new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
    frequency: StageFrequency.Monthly,
    dayOfMonth: 15,
    stageIndex: 0
);

// Calculate specific weekly payout
var payoutDate = DynamicStageCalculator.CalculateWeeklyPayoutDate(
    startDate: new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
    frequency: StageFrequency.Weekly,
    dayOfWeek: 1, // Monday
    stageIndex: 0
);
```

## Future Improvements

1. **Remove Wrapper Methods**: Consider removing the private wrapper methods in `InvestmentScriptBuilder` and calling `DynamicStageCalculator` directly

2. **Caching**: If stage dates are calculated frequently, consider caching results

3. **Additional Patterns**: Could add more payout patterns:
   - Last day of month
   - First/last weekday of month
   - Nth occurrence of a weekday (e.g., 2nd Tuesday)

4. **Validation**: Could add validation methods to check if a pattern configuration is valid before calculating dates

5. **UI Integration**: Use in UI to show preview of stage dates when creating/selecting patterns

## Migration Notes

- ? **Backward Compatible**: Existing code continues to work
- ? **No Breaking Changes**: All existing functionality preserved
- ? **Build Successful**: All projects compile without errors
- ? **Tests Passing**: All 25 new tests pass

## Files Modified

- ? Created: `..\Shared\Models\DynamicStageCalculator.cs`
- ? Created: `..\..\Angor.Test\Models\DynamicStageCalculatorTests.cs`
- ? Modified: `..\Shared\Protocol\Scripts\InvestmentScriptBuilder.cs`
- ? Modified: `..\Shared\Models\ProjectInfo.cs` (DynamicStageHelper)

## Summary

The refactoring successfully extracts stage calculation logic into a dedicated, well-tested class. This improves code quality, maintainability, and makes the calculation logic easily accessible throughout the codebase. All 25 comprehensive tests pass, ensuring the refactoring maintains existing functionality while improving code organization.

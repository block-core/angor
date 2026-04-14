# Angor Project Validation Analysis

## Overview
This document analyzes the validation system in the Avalonia project that will need to be ported to the new App project. The validation system includes both production and debug modes, with specific behaviors for each environment.

## Core Validation Components

### 1. Validation Environment System

**Location**: `src/avalonia/AngorApp/UI/Flows/CreateProject/Wizard/InvestmentProject/Model/ValidationEnvironment.cs`

The system uses an enum to distinguish between environments:
- `Production`: Full validation rules apply
- `Debug`: Reduced validation for testing purposes

### 2. Debug Mode Detection

**Location**: `src/avalonia/AngorApp/UI/Shared/Services/UIServices.cs`

Key method: `EnableProductionValidations()`
- Returns `true` when production validations should be enforced
- Returns `false` when debug mode is enabled AND network is testnet
- Logic: `!(isDebugMode && isTestnet)`

### 3. Debug Data Prefill

**Location**: `src/avalonia/AngorApp/UI/Flows/CreateProject/Wizard/FundProject/Model/DebugData.cs`

Provides default image URIs for debug mode:
- Uses picsum.photos service with random seeds
- Only active in DEBUG compilation mode
- Used for avatar and banner images in project creation

### 4. Debug Button Implementation

**Location**: `src/avalonia/AngorApp/UI/Flows/CreateProject/Wizard/InvestmentProject/ProjectProfileView.axaml`

The debug prefill button:
```xml
<EnhancedButton Content="Debug Prefill Data"
                Command="{Binding PrefillDebugData}"
                IsVisible="{Binding PrefillDebugData, Converter={x:Static ObjectConverters.IsNotNull}}"
                Classes="Emphasized"
                HorizontalAlignment="Right" />
```

**ViewModel**: `ProjectProfileViewModel.cs`
- Exposes `PrefillDebugData` command
- Command is only created when `prefillAction` is provided
- `prefillAction` is set based on debug mode detection

### 5. Project Configuration Validation

**Location**: `src/avalonia/AngorApp/UI/Flows/CreateProject/Wizard/InvestmentProject/Model/InvestmentProjectConfigBase.cs`

#### Environment-Specific Validation Rules:

**Production Validations** (lines 128-156):
- Target amount: 0.001 BTC minimum, 100 BTC maximum
- Penalty days: Minimum 10 days
- Funding end date: Must be after today, max 1 year period

**Debug Validations** (lines 158-161):
- Funding end date: Must be on or after today (more lenient)
- No minimum target amount
- No minimum penalty days
- No maximum funding period

#### Validation Environment Setup:
```csharp
private void AddEnvironmentSpecificValidations(ValidationEnvironment environment)
{
    if (environment == ValidationEnvironment.Production)
    {
        AddProductionValidations();
    }
    else
    {
        AddDebugValidations();
    }
}
```

### 6. Funding Stage Validation

**Location**: `src/avalonia/AngorApp/UI/Flows/CreateProject/Wizard/InvestmentProject/Model/FundingStageConfig.cs`

Environment-specific behavior:
- Debug mode: `minDaysAfterPrevious = 0` (allows same-day stages)
- Production mode: `minDaysAfterPrevious = 1` (requires at least 1 day between stages)

### 7. Debug Data Population

**Location**: `src/avalonia/AngorApp/UI/Flows/CreateProject/CreateProjectFlow.cs`

Debug data population methods:
- `PopulateInvestDebugDefaults()`: Creates test investment project with:
  - Auto-generated name with timestamp
  - Default website (angor.io)
  - Minimal target amount (0.01 BTC)
  - Zero penalty days
  - Immediate dates
  - Three stages with immediate release

- `PopulateFundDebugDefaults()`: Creates test fund project with:
  - Auto-generated name with timestamp
  - Default website (angor.io)
  - 0.5 BTC goal, 0.01 BTC threshold
  - Zero penalty days
  - Monthly payouts

### 8. Validation Service Integration

**Location**: `src/avalonia/AngorApp/UI/Shared/Services/Validations.cs`

Provides validation services:
- NIP-05 username validation
- Lightning address validation
- Image URL validation

## Key Implementation Patterns

### 1. Conditional Validation Based on Environment

```csharp
// In CreateProjectFlow.cs
var isDebug = !uiServices.EnableProductionValidations();
var environment = isDebug ? ValidationEnvironment.Debug : ValidationEnvironment.Production;
InvestmentProjectConfigBase newProject = isDebug 
    ? new InvestmentProjectConfigDebug() 
    : new InvestmentProjectConfig();
```

### 2. Debug Button Visibility Pattern

The debug button is conditionally visible:
- Only shown when `PrefillDebugData` command is not null
- Command is only created in debug mode
- Uses `ObjectConverters.IsNotNull` converter for visibility binding

### 3. Environment-Specific Project Classes

- `InvestmentProjectConfig`: Production configuration
- `InvestmentProjectConfigDebug`: Debug configuration
- Both inherit from `InvestmentProjectConfigBase`
- Environment passed to base constructor

## Validation Rules Summary

### Production Validation Rules

**Project Profile:**
- Name: Required, max 200 chars
- Description: Required, max 400 chars
- Website: Optional, must be valid URL if provided

**Funding Configuration:**
- Target amount: 0.001 - 100 BTC
- Penalty days: 10 - 365 days
- Funding end date: After today, within 1 year
- Start date: Required, before/equal to funding end

**Stages:**
- Total percentage: Must sum to 100%
- Individual percentages: Whole numbers only
- Release dates: At least 1 day after previous stage
- All stages must be valid

### Debug Validation Rules

**Project Profile:**
- Same as production (name, description, website)

**Funding Configuration:**
- Target amount: > 0 (no min/max)
- Penalty days: ≥ 0 (no min/max)
- Funding end date: On or after today (no max period)
- Start date: Required, before/equal to funding end

**Stages:**
- Total percentage: Must sum to 100%
- Individual percentages: Whole numbers only
- Release dates: Can be same day as previous stage
- All stages must be valid

## Implementation Requirements for App Project

### 1. Core Components to Port

1. **ValidationEnvironment enum**
2. **Debug mode detection logic** (`EnableProductionValidations()`)
3. **Debug data generation** (`DebugData.cs`)
4. **Environment-specific validation rules**
5. **Debug prefill button** with conditional visibility
6. **Debug data population methods**

### 2. Integration Points

1. **UI Services**: Debug mode detection and validation control
2. **Project Creation Flow**: Environment detection and config selection
3. **Project Profile View**: Debug button implementation
4. **Validation Services**: Image, NIP-05, Lightning validation
5. **Configuration Models**: Environment-specific validation rules

### 3. Testing Considerations

- Ensure debug mode only activates on testnet
- Verify debug button only shows in debug mode
- Test that debug validations are less restrictive
- Confirm debug data population works correctly
- Validate that production validations are enforced in production

## Recommendations

1. **Maintain Parity**: Keep validation rules identical between Avalonia and App projects
2. **Environment Safety**: Ensure debug mode cannot be accidentally enabled in production
3. **Testing**: Comprehensive testing of both validation modes
4. **Documentation**: Clear documentation of validation differences between modes
5. **Feature Flag**: Consider using feature flags for debug functionality

## Files to Reference

- `ValidationEnvironment.cs` - Environment enum
- `UIServices.cs` - Debug mode detection
- `DebugData.cs` - Debug data generation
- `InvestmentProjectConfigBase.cs` - Environment-specific validation
- `FundingStageConfig.cs` - Stage validation
- `CreateProjectFlow.cs` - Debug data population
- `ProjectProfileView.axaml` - Debug button UI
- `ProjectProfileViewModel.cs` - Debug button ViewModel
- `Validations.cs` - Validation services

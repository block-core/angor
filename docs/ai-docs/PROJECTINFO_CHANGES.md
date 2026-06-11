# ProjectInfo Changes - Dynamic Project Types

## Overview
The `ProjectInfo` class has been enhanced to support three different project types with varying requirements for stages, dates, penalties, and target amounts. A versioning system has been added to ensure backward compatibility with existing projects.

## Versioning

### Version History

| Version | Description | Date Added |
|---------|-------------|------------|
| **1** | Original schema - Single investment model with fixed stages | Legacy |
| **2** | Added ProjectType enum support for Invest/Fund/Subscribe models | Current |

### Version Property

```csharp
public int Version { get; set; } = 2;
```

- **New projects**: Default to `Version = 2`
- **Legacy projects**: Automatically detected as `Version = 1` (or explicitly set)
- **Future proofing**: New versions can be added as the schema evolves

### Legacy Compatibility

The system automatically handles Version 1 projects through helper properties:

- `IsLegacyVersion`: Returns `true` if `Version < 2`
- `EffectiveProjectType`: Returns `ProjectType.Invest` for legacy projects regardless of ProjectType value

All business logic should use `EffectiveProjectType` instead of `ProjectType` directly to ensure backward compatibility.

### Migration Strategy

**Version 1 ? Version 2:**
- No automatic migration required
- Version 1 projects continue to work as `Invest` type projects
- To upgrade: Set `Version = 2` and explicitly set `ProjectType`

**Example:**
```csharp
// Legacy project (Version 1)
var legacyProject = new ProjectInfo
{
    Version = 1, // Explicitly set for old projects
    // ProjectType is ignored
    TargetAmount = 100000000,
    StartDate = DateTime.UtcNow,
    // ... other properties
};

// This project behaves as ProjectType.Invest
Assert.Equal(ProjectType.Invest, legacyProject.EffectiveProjectType);
Assert.True(legacyProject.RequiresInvestmentWindow);
Assert.True(legacyProject.RequiresTargetAmount);

// Upgraded project
legacyProject.Version = 2;
legacyProject.ProjectType = ProjectType.Fund; // Now this takes effect
Assert.Equal(ProjectType.Fund, legacyProject.EffectiveProjectType);
```

## New Project Types

### 1. **Invest** (ProjectType.Invest = 0)
- **Purpose**: Traditional investment projects with fixed commitments
- **Characteristics**:
  - ? Fixed stages with predetermined dates and percentages
  - ? Required start and end dates (investment window)
  - ? Required target amount
  - ? Penalty applies for early withdrawal
  - ? Stages cannot be dynamically modified

**Use Case**: ICO-style projects where investors commit during a specific period, and funds are released according to a fixed schedule.

### 2. **Fund** (ProjectType.Fund = 1)
- **Purpose**: Flexible funding projects with evolving requirements
- **Characteristics**:
  - ? Dynamic stages that can be added/modified over time
  - ? No required start/end dates (no fixed investment window)
  - ?? Target amount is optional
  - ? Penalty applies for early withdrawal
  - ? Stages can be adjusted as project evolves

**Use Case**: Open-ended projects where funding needs may change, such as ongoing development projects or startups with evolving milestones.

### 3. **Subscribe** (ProjectType.Subscribe = 2)
- **Purpose**: Subscription-based or ongoing support projects
- **Characteristics**:
  - ? Dynamic stages similar to Fund type
  - ? No required start/end dates
  - ?? Target amount is optional
  - ? **No penalty** for withdrawal (key difference from Fund)
  - ? Stages can be adjusted over time

**Use Case**: Patreon-style projects, recurring donations, or community-supported projects where supporters can join/leave freely without penalties.

## New Properties

### `int Version { get; set; }`
Schema version for backward compatibility. Default is 2 for new projects.

### `ProjectType ProjectType { get; set; }`
Defines the type of project. Default is `ProjectType.Invest` for backward compatibility with existing projects.

### Helper Properties (Computed)

| Property | Description | Invest | Fund | Subscribe | Legacy (v1) |
|----------|-------------|--------|------|-----------|-------------|
| `IsLegacyVersion` | Is this a Version 1 project? | ? False | ? False | ? False | ? True |
| `EffectiveProjectType` | Actual project type (accounting for version) | Invest | Fund | Subscribe | Invest |
| `AllowDynamicStages` | Can stages be modified after creation? | ? False | ? True | ? True | ? False |
| `RequiresInvestmentWindow` | Must have start/end dates? | ? True | ? False | ? False | ? True |
| `HasPenalty` | Apply penalty for early withdrawal? | ? True | ? True | ? False | ? True |
| `RequiresTargetAmount` | Must have a target amount? | ? True | ? False | ? False | ? True |

## Enhanced Documentation

All properties now have comprehensive XML comments explaining:
- Purpose and usage
- Which project types require or use the property
- Default behavior for backward compatibility
- Version compatibility notes

## Backward Compatibility

? **Version 1 (Legacy) Projects:**
- Automatically detected via `Version < 2`
- Treated as `Invest` type regardless of `ProjectType` value
- All existing validation and business logic continues to work
- No migration required unless explicitly upgrading

? **Version 2 Projects:**
- Default for all new projects
- Full support for Invest/Fund/Subscribe types
- Explicit version management

? **No Breaking Changes:**
- All existing properties remain unchanged
- Default values ensure backward compatibility
- Legacy projects continue working without modification

## Example Usage

```csharp
// Traditional investment project (Version 2)
var investProject = new ProjectInfo
{
    Version = 2, // Explicit (though this is default)
    ProjectType = ProjectType.Invest,
  TargetAmount = 100000000, // Required
    StartDate = DateTime.UtcNow,
EndDate = DateTime.UtcNow.AddMonths(3),
    PenaltyDays = 90,
    Stages = new List<Stage>
    {
        new Stage { AmountToRelease = 33.33m, ReleaseDate = DateTime.UtcNow.AddMonths(6) },
  new Stage { AmountToRelease = 33.33m, ReleaseDate = DateTime.UtcNow.AddMonths(12) },
        new Stage { AmountToRelease = 33.34m, ReleaseDate = DateTime.UtcNow.AddMonths(18) }
    }
};

// Fund project with dynamic stages (Version 2)
var fundProject = new ProjectInfo
{
    Version = 2,
    ProjectType = ProjectType.Fund,
    TargetAmount = 0, // Optional
    PenaltyDays = 60,
    Stages = new List<Stage>() // Can be added dynamically later
};

// Subscribe project without penalties (Version 2)
var subscribeProject = new ProjectInfo
{
    Version = 2,
  ProjectType = ProjectType.Subscribe,
    PenaltyDays = 0, // No penalty
    Stages = new List<Stage>() // Can be added dynamically
};

// Legacy project (Version 1) - backward compatible
var legacyProject = new ProjectInfo
{
  Version = 1, // Or deserialized without Version property
    // ProjectType is ignored for Version 1
    TargetAmount = 100000000,
  StartDate = DateTime.UtcNow,
    EndDate = DateTime.UtcNow.AddMonths(3),
    PenaltyDays = 90,
    Stages = existingStages
};

// Check project characteristics (always use Effective properties for compatibility)
if (project.AllowDynamicStages)
{
    // Can add or modify stages
}

if (project.RequiresInvestmentWindow)
{
    // Must validate start and end dates
}

if (!project.HasPenalty)
{
    // Allow immediate withdrawal without penalty
}

// Always use EffectiveProjectType for business logic
switch (project.EffectiveProjectType)
{
    case ProjectType.Invest:
        // Handle invest-specific logic
        break;
    case ProjectType.Fund:
        // Handle fund-specific logic
        break;
 case ProjectType.Subscribe:
        // Handle subscribe-specific logic
    break;
}

// Check if project is legacy
if (project.IsLegacyVersion)
{
    // Handle legacy project migration or special processing
}
```

## Next Steps

### Recommended Updates:

1. **Version Detection & Migration**:
   - Implement version detection when deserializing projects
   - Add migration utilities for upgrading Version 1 ? Version 2
   - Log warnings for Version 1 projects
   - Provide UI option to upgrade legacy projects

2. **UI/UX Changes**:
   - Add project type selector in project creation form (Version 2 only)
   - Show version indicator in project details
   - Conditionally show/hide fields based on project type and version
   - Update validation logic for each project type
   - Add UI for dynamic stage management (Fund/Subscribe types)
   - Display "Legacy Project" badge for Version 1 projects

3. **Validation Updates**:
   - Use `EffectiveProjectType` instead of `ProjectType` directly
   - Modify validation to respect `RequiresInvestmentWindow`
   - Make target amount optional for Fund/Subscribe types
   - Adjust penalty validation based on `HasPenalty`
   - Allow stage modification for Fund/Subscribe types
   - Add version-aware validation rules

4. **Business Logic Updates**:
   - Always use `EffectiveProjectType` for type checking
   - Implement dynamic stage addition/modification for Fund/Subscribe
   - Update penalty calculation to check `HasPenalty`
   - Adjust investment window checks using `RequiresInvestmentWindow`
   - Update withdrawal logic for Subscribe type (no penalties)
   - Handle Version 1 projects appropriately in all workflows

5. **Database/Storage**:
   - Ensure `Version` property is properly serialized
   - Ensure `ProjectType` enum is properly serialized
   - Update indexer to support new project types and versions
   - Implement migration for existing projects (set Version = 1 if not present)
   - Add version field to database indexes

6. **API Compatibility**:
   - Nostr events should include version information
   - API responses should indicate project version
   - Support both Version 1 and Version 2 in API endpoints
   - Document version requirements in API documentation

7. **Testing**:
   - Unit tests for each project type
   - Version migration tests (V1 ? V2)
   - Legacy project compatibility tests
   - Validation tests for required/optional fields per version
   - Integration tests for dynamic stages
   - Penalty/no-penalty scenarios
   - Test version detection and EffectiveProjectType

8. **Documentation**:
   - Update user documentation with version information
   - Add migration guide for Version 1 ? Version 2
   - Document version compatibility in API docs
   - Add troubleshooting for version-related issues

## Benefits

? **Flexibility**: Support multiple funding models within the same system  
? **Clarity**: Clear distinction between project types and their requirements  
? **Extensibility**: Easy to add new project types or versions in the future  
? **Backward Compatible**: Existing projects continue to work without changes  
? **Self-Documenting**: Helper properties make code more readable  
? **Future-Proof**: Version system allows for schema evolution  
? **Safe Migration**: Legacy projects clearly identified and handled appropriately  

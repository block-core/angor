# Validation Implementation Plan for App Project

## Phase 1: Architecture Design (1-2 days)

### Objective: Design the validation architecture for the App project

**Tasks:**
1. **Review Avalonia validation architecture** ✓
   - Analyze existing patterns and components
   - Identify dependencies and integration points

2. **Design App project validation architecture**
   - Create component diagram showing validation flow
   - Define interfaces and base classes
   - Determine where validation logic will reside

3. **Establish naming conventions**
   - Follow existing App project naming patterns
   - Ensure consistency with Avalonia where appropriate

4. **Define environment strategy**
   - How debug mode will be detected
   - How validation rules will switch between environments
   - Security considerations for debug mode

**Deliverables:**
- Architecture diagram
- Component interaction documentation
- Decision records for key design choices

## Phase 2: Core Infrastructure (2-3 days)

### Objective: Implement the foundational validation components

**Tasks:**

1. **Create ValidationEnvironment enum**
   ```csharp
   // Location: src/app/AngorApp/UI/Flows/CreateProject/ValidationEnvironment.cs
   public enum ValidationEnvironment
   {
       Production,
       Debug
   }
   ```

2. **Enhance UI Services with debug detection**
   ```csharp
   // Add to existing UIServices class
   public bool EnableProductionValidations()
   {
       var isDebugMode = IsDebugModeEnabled;
       var network = networkConfiguration.GetNetwork();
       var isTestnet = network.NetworkType == NetworkType.Testnet;
       return !(isDebugMode && isTestnet);
   }
   ```

3. **Create DebugData utility class**
   ```csharp
   // Location: src/app/AngorApp/UI/Flows/CreateProject/DebugData.cs
   public static class DebugData
   {
       public static string GetDefaultImageUriString(int width, int height)
       {
   #if DEBUG
           var seed = Guid.NewGuid().ToString("N")[..8];
           return $"https://picsum.photos/seed/{seed}/{width}/{height}";
   #else
           return "";
   #endif
       }
   }
   ```

4. **Create base validation interfaces**
   ```csharp
   // Location: src/app/AngorApp/UI/Shared/Services/IValidations.cs
   public interface IValidations
   {
       Task<Result> CheckNip05Username(string username, string nostrPubKey);
       Task<Result> CheckLightningAddress(string lightningAddress);
       Task<Result<bool>> IsImage(string url);
   }
   ```

**Deliverables:**
- ValidationEnvironment enum implementation
- Enhanced UIServices with debug detection
- DebugData utility class
- Validation service interfaces

## Phase 3: Project Configuration Validation (3-4 days)

### Objective: Implement environment-specific validation for project creation

**Tasks:**

1. **Create InvestmentProjectConfigBase**
   ```csharp
   // Location: src/app/AngorApp/UI/Flows/CreateProject/Model/InvestmentProjectConfigBase.cs
   public abstract class InvestmentProjectConfigBase : ReactiveValidationObject, IInvestmentProjectConfig
   {
       protected ValidationEnvironment Environment { get; }
       
       protected InvestmentProjectConfigBase(ValidationEnvironment environment)
       {
           Environment = environment;
           AddCommonValidations();
           AddEnvironmentSpecificValidations(environment);
       }
       
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
   }
   ```

2. **Implement production validation rules**
   ```csharp
   private void AddProductionValidations()
   {
       // Target amount: 0.001 - 100 BTC
       this.ValidationRule(
           this.WhenAnyValue(x => x.TargetAmount, x => x.TargetAmount!.Sats,
               (amount, sats) => amount == null || (sats >= 100_000 && sats <= 10_000_000_000)),
           isValid => isValid,
           _ => "Target amount must be between 0.001 and 100 BTC.");
       
       // Penalty days: 10 - 365 days
       this.ValidationRule(x => x.PenaltyDays, x => x is null || (x >= 10 && x <= 365),
           "Penalty period must be between 10 and 365 days.");
       
       // Funding period: within 1 year
       this.ValidationRule(x => x.FundingEndDate,
           x => x is null || (x.Value - DateTime.Now) <= TimeSpan.FromDays(365),
           "Funding period cannot exceed one year.");
   }
   ```

3. **Implement debug validation rules**
   ```csharp
   private void AddDebugValidations()
   {
       // Target amount: > 0 (no min/max)
       this.ValidationRule(x => x.TargetAmount, x => x is null || x.Sats > 0,
           "Target amount must be greater than 0.");
       
       // Penalty days: >= 0 (no max)
       this.ValidationRule(x => x.PenaltyDays, x => x is null || x >= 0,
           "Penalty days cannot be negative.");
       
       // Funding end date: on or after today
       this.ValidationRule(x => x.FundingEndDate,
           x => x is null || x.Value.Date >= DateTime.Now.Date,
           "Funding end date must be on or after today.");
   }
   ```

4. **Create concrete configuration classes**
   ```csharp
   // Production configuration
   public class InvestmentProjectConfig : InvestmentProjectConfigBase
   {
       public InvestmentProjectConfig() : base(ValidationEnvironment.Production) { }
   }
   
   // Debug configuration  
   public class InvestmentProjectConfigDebug : InvestmentProjectConfigBase
   {
       public InvestmentProjectConfigDebug() : base(ValidationEnvironment.Debug) { }
   }
   ```

**Deliverables:**
- InvestmentProjectConfigBase with environment-specific validation
- Production and debug configuration classes
- Comprehensive validation rules for all project fields

## Phase 4: Funding Stage Validation (2 days)

### Objective: Implement validation for funding stages

**Tasks:**

1. **Create FundingStageConfig with environment awareness**
   ```csharp
   // Location: src/app/AngorApp/UI/Flows/CreateProject/Model/FundingStageConfig.cs
   public class FundingStageConfig : ReactiveValidationObject, IFundingStageConfig
   {
       public FundingStageConfig(ValidationEnvironment environment = ValidationEnvironment.Production)
       {
           var minDaysAfterPrevious = environment == ValidationEnvironment.Debug ? 0 : 1;
           
           // Release date validation with environment-specific minimum days
           var minAllowed = previousDate.Select(d => d.AddDays(minDaysAfterPrevious));
           
           this.ValidationRule(stage => stage.ReleaseDate,
               this.WhenAnyValue(x => x.ReleaseDate)
                   .CombineLatest(minAllowed, (relDate, minDate) => new { relDate, minDate }),
               arg => arg.relDate == null || arg.relDate.Value.Date >= arg.minDate,
               arg => environment == ValidationEnvironment.Debug 
                   ? "Release date must be on or after the minimum allowed date."
                   : "Release date must be at least 1 day after the previous stage.");
       }
   }
   ```

2. **Implement stage percentage validation**
   ```csharp
   // In InvestmentProjectConfigBase
   var totalPercent = StagesSource.Connect()
       .AutoRefresh(x => x.Percent)
       .ToCollection()
       .Select(items => items.Sum(x => x.Percent ?? 0));
   
   this.ValidationRule(x => x.Stages, 
       totalPercent.Select(percent => Math.Abs(percent - 1.0m) < 0.0001m),
       "Total percentage must be 100%");
   ```

**Deliverables:**
- FundingStageConfig with environment-specific validation
- Stage percentage validation
- Integration with project configuration

## Phase 5: Debug Data Population (1-2 days)

### Objective: Implement debug data prefill functionality

**Tasks:**

1. **Create debug data population methods**
   ```csharp
   // In CreateProjectFlow.cs
   private static void PopulateInvestDebugDefaults(InvestmentProjectConfigBase project)
   {
       var id = Guid.NewGuid().ToString()[..8];
       project.Name = $"Debug Project {id}";
       project.Description = $"Auto-populated debug project {id} for testing on testnet. Created at {DateTime.Now:HH:mm:ss}.";
       project.Website = "https://angor.io";
       project.TargetAmount = AmountUI.FromBtc(0.01);
       project.PenaltyDays = 0;
       project.StartDate = DateTime.Now.Date;
       project.FundingEndDate = DateTime.Now.Date;
       project.ExpiryDate = DateTime.Now.Date.AddDays(31);
       
       // Add stages with dates matching the funding end date for immediate release
       project.CreateAndAddStage(0.10m, DateTime.Now.Date);
       project.CreateAndAddStage(0.30m, DateTime.Now.Date);
       project.CreateAndAddStage(0.60m, DateTime.Now.Date);
   }
   ```

2. **Integrate debug prefill with project creation flow**
   ```csharp
   // In CreateProjectFlow.cs
   private SlimWizard<string> CreateInvestmentProjectWizard(WalletId walletId, ProjectSeedDto seed)
   {
       var isDebug = !uiServices.EnableProductionValidations();
       var environment = isDebug ? ValidationEnvironment.Debug : ValidationEnvironment.Production;
       InvestmentProjectConfigBase newProject = isDebug 
           ? new InvestmentProjectConfigDebug() 
           : new InvestmentProjectConfig();

       Action? prefillAction = isDebug ? () => PopulateInvestDebugDefaults(newProject) : null;

       // Pass prefillAction to ProjectProfileViewModel
       SlimWizard<string> wizard = WizardBuilder
           .StartWith(() => new ProjectProfileViewModel(newProject, prefillAction))
           // ... rest of wizard setup
   }
   ```

**Deliverables:**
- Debug data population methods for investment and fund projects
- Integration with project creation wizard
- Conditional debug prefill based on environment

## Phase 6: UI Integration (2-3 days)

### Objective: Add debug button and integrate validation with UI

**Tasks:**

1. **Add debug button to ProjectProfileView**
   ```xml
   <!-- In ProjectProfileView.axaml -->
   <EnhancedButton Content="Debug Prefill Data"
                   Command="{Binding PrefillDebugData}"
                   IsVisible="{Binding PrefillDebugData, Converter={x:Static ObjectConverters.IsNotNull}}"
                   Classes="Emphasized"
                   HorizontalAlignment="Right" />
   ```

2. **Implement ProjectProfileViewModel with debug support**
   ```csharp
   public class ProjectProfileViewModel : IHaveTitle, IValidatable
   {
       public ProjectProfileViewModel(IProjectProfile newProject, Action? prefillAction = null)
       {
           NewProject = newProject;
           
           if (prefillAction is not null)
           {
               PrefillDebugData = ReactiveCommand.Create(() => prefillAction());
           }
       }
       
       public IProjectProfile NewProject { get; }
       public ICommand? PrefillDebugData { get; }
       
       public IObservable<string> Title => Observable.Return("Project Profile");
       public IObservable<bool> IsValid => NewProject.WhenValid(
           x => x.Name,
           x => x.Description,
           x => x.Website);
   }
   ```

3. **Add validation error display**
   ```xml
   <!-- Add ErrorSummary control to views -->
   <controls:ErrorSummary Errors="{Binding NewProject.ValidationContext.Errors}" />
   ```

**Deliverables:**
- Debug button in project profile view
- ViewModel integration with debug prefill
- Validation error display in UI
- Responsive validation feedback

## Phase 7: Validation Services (2 days)

### Objective: Implement validation services for external validations

**Tasks:**

1. **Implement Validations service**
   ```csharp
   // Location: src/app/AngorApp/UI/Shared/Services/Validations.cs
   public class Validations : IValidations
   {
       private readonly Nip05Validator nip05Validator;
       private readonly LightningAddressValidator lightningAddressValidator;
       private readonly IImageValidationService imageValidationService;
       
       public Validations(IHttpClientFactory httpClientFactory, IImageValidationService imageValidationService)
       {
           nip05Validator = new Nip05Validator(httpClientFactory);
           lightningAddressValidator = new LightningAddressValidator(httpClientFactory);
           this.imageValidationService = imageValidationService;
       }
       
       public Task<Result> CheckNip05Username(string username, string nostrPubKey)
       {
           return nip05Validator.CheckNip05Username(username, nostrPubKey);
       }
       
       public Task<Result> CheckLightningAddress(string lightningAddress)
       {
           return lightningAddressValidator.CheckLightningAddress(lightningAddress);
       }
       
       public Task<Result<bool>> IsImage(string url)
       {
           return imageValidationService.IsImage(url);
       }
   }
   ```

2. **Create validators (NIP-05, Lightning, Image)**
   - Port existing validators from Avalonia project
   - Ensure proper error handling
   - Add appropriate logging

**Deliverables:**
- Validations service implementation
- NIP-05 validator
- Lightning address validator
- Image validation service

## Phase 8: Testing (3-4 days)

### Objective: Comprehensive testing of validation system

**Tasks:**

1. **Unit Tests**
   - Test validation rules in both environments
   - Test debug mode detection logic
   - Test validation service methods
   - Test debug data population

2. **Integration Tests**
   - Test validation in project creation flow
   - Test debug button functionality
   - Test environment switching

3. **UI Tests**
   - Test validation error display
   - Test debug button visibility
   - Test responsive validation feedback

4. **Edge Case Testing**
   - Boundary conditions for validation rules
   - Environment switching during project creation
   - Network changes affecting debug mode

**Deliverables:**
- Comprehensive test suite
- Test coverage reports
- Bug fixes for any issues found

## Phase 9: Documentation and Finalization (1-2 days)

### Objective: Complete documentation and finalize implementation

**Tasks:**

1. **Update architecture documentation**
   - Add validation system to overall architecture
   - Document component interactions
   - Create sequence diagrams for key flows

2. **Create user documentation**
   - Debug mode usage guide
   - Validation rules reference
   - Troubleshooting guide

3. **Code review and cleanup**
   - Ensure consistent coding style
   - Add missing comments
   - Optimize performance where needed

4. **Final testing and validation**
   - End-to-end testing
   - Performance testing
   - Security review

**Deliverables:**
- Complete documentation
- Clean, reviewed code
- Final test results
- Ready for production deployment

## Implementation Timeline

| Phase | Duration | Start Date | End Date |
|-------|----------|------------|----------|
| 1. Architecture Design | 1-2 days | Day 1 | Day 2 |
| 2. Core Infrastructure | 2-3 days | Day 3 | Day 5 |
| 3. Project Configuration | 3-4 days | Day 6 | Day 9 |
| 4. Funding Stage Validation | 2 days | Day 10 | Day 11 |
| 5. Debug Data Population | 1-2 days | Day 12 | Day 13 |
| 6. UI Integration | 2-3 days | Day 14 | Day 16 |
| 7. Validation Services | 2 days | Day 17 | Day 18 |
| 8. Testing | 3-4 days | Day 19 | Day 22 |
| 9. Documentation | 1-2 days | Day 23 | Day 24 |
| **Total** | **18-24 days** | **Day 1** | **Day 24** |

## Risk Assessment and Mitigation

### Risks:
1. **Validation rule inconsistencies**: Different rules between Avalonia and App projects
   - *Mitigation*: Create shared validation rule library or ensure manual parity

2. **Debug mode security issues**: Debug mode accidentally enabled in production
   - *Mitigation*: Multiple safeguards, thorough testing, code reviews

3. **Performance issues**: Validation impacting UI responsiveness
   - *Mitigation*: Profile validation performance, optimize where needed

4. **Integration challenges**: Validation not integrating well with existing App architecture
   - *Mitigation*: Early prototyping, frequent integration testing

## Success Criteria

1. **Validation works correctly** in both production and debug modes
2. **Debug button** appears only in debug mode and functions correctly
3. **Validation rules** match Avalonia project requirements
4. **Performance** is acceptable (validation < 100ms for typical operations)
5. **Code quality** meets project standards (tests, documentation, style)
6. **User experience** is smooth with clear validation feedback

## Next Steps

1. Begin Phase 1: Architecture Design
2. Set up regular progress reviews
3. Establish testing strategy early
4. Plan for code reviews and knowledge sharing
5. Monitor progress against timeline

// SIMPLE VALIDATION IMPLEMENTATION EXAMPLE
// This shows how to implement validation with minimal code changes

using System;
using Angor.Sdk.Common;

// ============================================
// 1. SIMPLE VALIDATION RESULT CLASS
// ============================================

public class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    
    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
    
    public static ValidationResult Success() => new ValidationResult(true, null);
    public static ValidationResult Fail(string errorMessage) => new ValidationResult(false, errorMessage);
    
    // Helper for combining multiple validations
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        foreach (var result in results)
        {
            if (!result.IsValid)
                return result;
        }
        return Success();
    }
}

// ============================================
// 2. SIMPLE PROJECT VALIDATOR CLASS
// ============================================

public class ProjectValidator
{
    private readonly bool _isDebugMode;
    
    public ProjectValidator(bool isDebugMode)
    {
        _isDebugMode = isDebugMode;
    }
    
    // Validate project name
    public ValidationResult ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ValidationResult.Fail("Project name is required");
        
        if (name.Length > 200)
            return ValidationResult.Fail("Project name cannot exceed 200 characters");
        
        return ValidationResult.Success();
    }
    
    // Validate project description
    public ValidationResult ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return ValidationResult.Fail("Project description is required");
        
        if (description.Length > 400)
            return ValidationResult.Fail("Description cannot exceed 400 characters");
        
        return ValidationResult.Success();
    }
    
    // Validate website URL
    public ValidationResult ValidateWebsite(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
            return ValidationResult.Success(); // Website is optional
        
        if (!Uri.TryCreate(website, UriKind.Absolute, out var uri))
            return ValidationResult.Fail("Website must be a valid URL");
        
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return ValidationResult.Fail("Website must use http or https");
        
        return ValidationResult.Success();
    }
    
    // Validate target amount with environment-specific rules
    public ValidationResult ValidateTargetAmount(decimal amount)
    {
        if (amount <= 0)
            return ValidationResult.Fail("Target amount must be greater than 0");
        
        // Debug mode: no min/max limits
        if (_isDebugMode)
            return ValidationResult.Success();
        
        // Production mode: enforce limits
        if (amount < 0.001m)
            return ValidationResult.Fail("Target amount must be at least 0.001 BTC");
        
        if (amount > 100m)
            return ValidationResult.Fail("Target amount cannot exceed 100 BTC");
        
        return ValidationResult.Success();
    }
    
    // Validate penalty days with environment-specific rules
    public ValidationResult ValidatePenaltyDays(int days)
    {
        if (days < 0)
            return ValidationResult.Fail("Penalty days cannot be negative");
        
        // Debug mode: no minimum
        if (_isDebugMode)
            return ValidationResult.Success();
        
        // Production mode: enforce minimum
        if (days < 10)
            return ValidationResult.Fail("Penalty period must be at least 10 days");
        
        if (days > 365)
            return ValidationResult.Fail("Penalty period cannot exceed 365 days");
        
        return ValidationResult.Success();
    }
    
    // Validate funding end date with environment-specific rules
    public ValidationResult ValidateFundingEndDate(DateTime endDate)
    {
        if (endDate.Date < DateTime.Now.Date)
            return ValidationResult.Fail("Funding end date must be on or after today");
        
        // Debug mode: no maximum period
        if (_isDebugMode)
            return ValidationResult.Success();
        
        // Production mode: enforce maximum period
        var daysUntilEnd = (endDate - DateTime.Now).TotalDays;
        if (daysUntilEnd > 365)
            return ValidationResult.Fail("Funding period cannot exceed one year");
        
        return ValidationResult.Success();
    }
    
    // Validate start date
    public ValidationResult ValidateStartDate(DateTime startDate)
    {
        if (startDate.Date < DateTime.Now.Date)
            return ValidationResult.Fail("Start date cannot be in the past");
        
        return ValidationResult.Success();
    }
    
    // Validate that start date is before or equal to funding end date
    public ValidationResult ValidateDateOrder(DateTime startDate, DateTime endDate)
    {
        if (startDate > endDate)
            return ValidationResult.Fail("Start date must be before or equal to funding end date");
        
        return ValidationResult.Success();
    }
    
    // Validate stages sum to 100%
    public ValidationResult ValidateStagePercentages(decimal[] percentages)
    {
        var total = percentages.Sum();
        
        if (Math.Abs(total - 100m) > 0.01m)
            return ValidationResult.Fail("Total percentage must be 100%");
        
        // Check each percentage is a whole number
        foreach (var percent in percentages)
        {
            if (percent != Math.Truncate(percent))
                return ValidationResult.Fail("Stage percentages must be whole numbers");
        }
        
        return ValidationResult.Success();
    }
    
    // Validate stage release dates (environment-specific)
    public ValidationResult ValidateStageReleaseDates(DateTime[] releaseDates, int minDaysBetween = 1)
    {
        if (releaseDates.Length == 0)
            return ValidationResult.Success();
        
        // Debug mode allows same-day stages
        if (_isDebugMode && minDaysBetween == 1)
            minDaysBetween = 0;
        
        for (int i = 1; i < releaseDates.Length; i++)
        {
            var daysBetween = (releaseDates[i] - releaseDates[i-1]).TotalDays;
            if (daysBetween < minDaysBetween)
                return ValidationResult.Fail($"Stage {i+1} must be at least {minDaysBetween} day(s) after previous stage");
        }
        
        return ValidationResult.Success();
    }
}

// ============================================
// 3. SIMPLE DEBUG DATA HELPER
// ============================================

public static class DebugDataHelper
{
    public static void PopulateInvestmentProject(InvestmentProjectConfig project)
    {
        var id = Guid.NewGuid().ToString()[..8];
        project.Name = $"Debug Project {id}";
        project.Description = $"Auto-populated debug project {id} for testing. Created at {DateTime.Now:HH:mm:ss}.";
        project.Website = "https://angor.io";
        project.TargetAmount = 0.01m; // Minimal amount for testing
        project.PenaltyDays = 0;
        project.StartDate = DateTime.Now.Date;
        project.FundingEndDate = DateTime.Now.Date;
        project.ExpiryDate = DateTime.Now.Date.AddDays(31);
    }
    
    public static void PopulateFundProject(FundProjectConfig project)
    {
        var id = Guid.NewGuid().ToString()[..8];
        project.Name = $"Debug Fund {id}";
        project.Description = $"Auto-populated debug fund {id} for testing. Created at {DateTime.Now:HH:mm:ss}.";
        project.Website = "https://angor.io";
        project.GoalAmount = 0.5m;
        project.Threshold = 0.01m;
        project.PenaltyDays = 0;
    }
}

// ============================================
// 4. USAGE EXAMPLE IN VIEWMODEL
// ============================================

public class CreateProjectViewModel
{
    private readonly ProjectValidator _validator;
    private readonly bool _isDebugMode;
    
    public CreateProjectViewModel(bool isDebugMode)
    {
        _isDebugMode = isDebugMode;
        _validator = new ProjectValidator(isDebugMode);
    }
    
    // Simple validation method
    public ValidationResult ValidateProject(Project project)
    {
        var results = new[]
        {
            _validator.ValidateName(project.Name),
            _validator.ValidateDescription(project.Description),
            _validator.ValidateWebsite(project.Website),
            _validator.ValidateTargetAmount(project.TargetAmount),
            _validator.ValidatePenaltyDays(project.PenaltyDays),
            _validator.ValidateStartDate(project.StartDate),
            _validator.ValidateFundingEndDate(project.FundingEndDate),
            _validator.ValidateDateOrder(project.StartDate, project.FundingEndDate)
        };
        
        return ValidationResult.Combine(results);
    }
    
    // Debug data population
    public void PopulateDebugData(Project project)
    {
        if (project is InvestmentProjectConfig investProject)
        {
            DebugDataHelper.PopulateInvestmentProject(investProject);
        }
        else if (project is FundProjectConfig fundProject)
        {
            DebugDataHelper.PopulateFundProject(fundProject);
        }
    }
    
    // Example usage in command
    public async Task CreateProjectCommand()
    {
        var validation = ValidateProject(CurrentProject);
        
        if (!validation.IsValid)
        {
            // Show error to user
            ShowError(validation.ErrorMessage);
            return;
        }
        
        // Proceed with project creation
        var result = await _projectService.CreateProject(CurrentProject);
        
        if (result.IsSuccess)
        {
            ShowSuccess("Project created successfully!");
        }
        else
        {
            ShowError(result.Error);
        }
    }
}

// ============================================
// 5. DEBUG BUTTON IN XAML (SIMPLE VERSION)
// ============================================

/*
<Button Content="Debug: Fill Test Data"
        Command="{Binding PopulateDebugDataCommand}"
        Visibility="{Binding IsDebugMode, Converter={StaticResource BoolToVisibilityConverter}}"
        Style="{StaticResource EmphasizedButtonStyle}" />
*/

// ============================================
// 6. DEBUG MODE DETECTION
// ============================================

// In your UI services or similar:
public bool IsDebugMode => IsDebugModeEnabled && CurrentNetwork.NetworkType == NetworkType.Testnet;

// ============================================
// BENEFITS OF THIS APPROACH
// ============================================

// 1. MINIMAL CODE: Only 2-3 new files needed
// 2. SIMPLE: Easy to understand and maintain
// 3. FAST: Can implement in 1-2 days
// 4. FLEXIBLE: Easy to add new validation rules
// 5. TESTABLE: Simple methods are easy to test
// 6. REUSABLE: Can use validator anywhere in the app

// ============================================
// IMPLEMENTATION STEPS
// ============================================

// 1. Create ProjectValidator.cs (1-2 hours)
// 2. Create DebugDataHelper.cs (30 minutes)
// 3. Add debug mode detection (15 minutes)
// 4. Integrate with existing ViewModels (1-2 hours)
// 5. Add debug button to UI (30 minutes)
// 6. Test (2-4 hours)

// TOTAL: 1-2 days maximum!

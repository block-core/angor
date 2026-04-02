namespace App.UI.Shared;

/// <summary>
/// Centralized project-type terminology mappings.
/// Extracted from 5 ViewModels that each had their own copy of these switch expressions.
/// All strings match the Vue JS bundle exactly.
/// 
/// Two naming conventions are supported:
///   - PascalCase methods: for VMs using "Invest"/"Fund"/"Subscription" (FindProjects, Portfolio)
///   - The ProjectType enum can be obtained from either convention via extension methods.
/// </summary>
public static class ProjectTypeTerminology
{
    // ── Display-facing terminology (used in detail pages, cards, headers) ──

    public static string OpportunityTitle(ProjectType type) => type switch
    {
        ProjectType.Fund => "Funding Opportunity",
        ProjectType.Subscription => "Subscription Opportunity",
        _ => "Investment Opportunity"
    };

    public static string ActionButtonText(ProjectType type) => type switch
    {
        ProjectType.Fund => "Fund This Project",
        ProjectType.Subscription => "Subscribe Now",
        _ => "Invest Now"
    };

    public static string InfoSectionTitle(ProjectType type) => type switch
    {
        ProjectType.Fund => "Funding Information",
        ProjectType.Subscription => "Subscription Information",
        _ => "Investment Information"
    };

    /// <summary>"Total Funders" / "Total Subscribers" / "Total Investors"</summary>
    public static string InvestorNounTotal(ProjectType type) => type switch
    {
        ProjectType.Fund => "Total Funders",
        ProjectType.Subscription => "Total Subscribers",
        _ => "Total Investors"
    };

    /// <summary>"Funders" / "Subscribers" / "Investors" (plural, no "Total")</summary>
    public static string InvestorNounPlural(ProjectType type) => type switch
    {
        ProjectType.Fund => "Funders",
        ProjectType.Subscription => "Subscribers",
        _ => "Investors"
    };

    public static string TargetNoun(ProjectType type) => type switch
    {
        ProjectType.Fund => "Goal Amount",
        ProjectType.Subscription => "Goal Amount",
        _ => "Target Amount"
    };

    public static string RaisedNoun(ProjectType type) => type switch
    {
        ProjectType.Fund => "Total Funded",
        ProjectType.Subscription => "Total Subscribed",
        _ => "Total Raised"
    };

    // ── Card pill labels ──

    /// <summary>Pill text: "Investment" / "Fund" / "Subscription"</summary>
    public static string TypePillText(ProjectType type) => type switch
    {
        ProjectType.Fund => "Fund",
        ProjectType.Subscription => "Subscription",
        _ => "Investment"
    };

    // ── Create wizard terminology ──

    /// <summary>"Fund" / "Subscribe" / "Invest"</summary>
    public static string ActionVerb(ProjectType type) => type switch
    {
        ProjectType.Fund => "Fund",
        ProjectType.Subscription => "Subscribe",
        _ => "Invest"
    };

    /// <summary>"Funding" / "Subscription" / "Investment"</summary>
    public static string AmountNoun(ProjectType type) => type switch
    {
        ProjectType.Fund => "Funding",
        ProjectType.Subscription => "Subscription",
        _ => "Investment"
    };

    /// <summary>"Payment" / "Stage"</summary>
    public static string StageLabel(ProjectType type) => type switch
    {
        ProjectType.Fund or ProjectType.Subscription => "Payment",
        _ => "Stage"
    };

    /// <summary>"Payment Schedule" / "Release Schedule"</summary>
    public static string ScheduleTitle(ProjectType type) => type switch
    {
        ProjectType.Fund or ProjectType.Subscription => "Payment Schedule",
        _ => "Release Schedule"
    };

    /// <summary>"Goal:" / "Subscribers:" / "Target:"</summary>
    public static string TargetLabel(ProjectType type) => type switch
    {
        ProjectType.Fund => "Goal:",
        ProjectType.Subscription => "Subscribers:",
        _ => "Target:"
    };

    /// <summary>"Goal Amount" / "Subscription Price" / "Target Amount" (for create wizard step 4)</summary>
    public static string TargetLabelFull(ProjectType type) => type switch
    {
        ProjectType.Fund => "Goal Amount",
        ProjectType.Subscription => "Subscription Price",
        _ => "Target Amount"
    };

    /// <summary>Step 4 title in wizard: "Goal" / "Subscription Price" / "Funding Configuration"</summary>
    public static string Step4Title(ProjectType type) => type switch
    {
        ProjectType.Fund => "Goal",
        ProjectType.Subscription => "Subscription Price",
        _ => "Funding Configuration"
    };

    /// <summary>Step 5 title in wizard: "Payouts" / "Stages"</summary>
    public static string Step5Title(ProjectType type) => type switch
    {
        ProjectType.Fund or ProjectType.Subscription => "Payouts",
        _ => "Stages"
    };

    // ── Success messages (invest flow) ──

    public static string SuccessTitle(ProjectType type, bool isAutoApproved) => isAutoApproved
        ? type switch
        {
            ProjectType.Fund => "Funding Successful",
            ProjectType.Subscription => "Subscription Successful",
            _ => "Investment Successful"
        }
        : type switch
        {
            ProjectType.Fund => "Funding Pending Approval",
            ProjectType.Subscription => "Subscription Pending Approval",
            _ => "Investment Pending Approval"
        };

    public static string SuccessDescription(ProjectType type, bool isAutoApproved, string formattedAmount, string symbol, string projectName) => isAutoApproved
        ? $"Your {type.ToDisplayString().ToLower()} of {formattedAmount} {symbol} to {projectName} has been published successfully."
        : $"Your {type.ToDisplayString().ToLower()} of {formattedAmount} {symbol} to {projectName} has been submitted and is pending founder approval.";

    public static string SuccessButtonText(ProjectType type) => type switch
    {
        ProjectType.Fund => "View My Fundings",
        ProjectType.Subscription => "View My Subscriptions",
        _ => "View My Investments"
    };
}

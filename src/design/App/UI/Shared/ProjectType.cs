namespace App.UI.Shared;

/// <summary>
/// Canonical project type enum replacing magic strings throughout the codebase.
/// Two casing conventions existed:
///   - PascalCase ("Invest", "Fund", "Subscription") in FindProjects/Portfolio ViewModels
///   - lowercase ("investment", "fund", "subscription") in CreateProject/MyProjects ViewModels
/// This enum unifies both via the extension methods below.
/// </summary>
public enum ProjectType
{
    Investment,
    Fund,
    Subscription
}

public static class ProjectTypeExtensions
{
    /// <summary>
    /// Parse from the PascalCase convention used in FindProjects/Portfolio VMs.
    /// "Invest" | "Fund" | "Subscription" → enum.
    /// </summary>
    public static ProjectType FromDisplayString(string value) => value switch
    {
        "Fund" => ProjectType.Fund,
        "Subscription" => ProjectType.Subscription,
        _ => ProjectType.Investment
    };

    /// <summary>
    /// Parse from the lowercase convention used in CreateProject/MyProjects VMs.
    /// "investment" | "fund" | "subscription" → enum.
    /// </summary>
    public static ProjectType FromLowerString(string value) => value switch
    {
        "fund" => ProjectType.Fund,
        "subscription" => ProjectType.Subscription,
        _ => ProjectType.Investment
    };

    /// <summary>PascalCase display string: "Invest", "Fund", "Subscription".</summary>
    public static string ToDisplayString(this ProjectType type) => type switch
    {
        ProjectType.Fund => "Fund",
        ProjectType.Subscription => "Subscription",
        _ => "Invest"
    };

    /// <summary>Lowercase string: "investment", "fund", "subscription".</summary>
    public static string ToLowerString(this ProjectType type) => type switch
    {
        ProjectType.Fund => "fund",
        ProjectType.Subscription => "subscription",
        _ => "investment"
    };
}

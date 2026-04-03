using System;
using AngorApp.Services;

namespace AngorApp.UI.Flows.CreateProject;

/// <summary>
/// Helper class for populating debug/test data in project creation.
/// </summary>
public static class DebugDataHelper
{
    /// <summary>
    /// Populates an investment project with test data for debugging.
    /// </summary>
    public static void PopulateInvestmentProject(dynamic project)
    {
        try
        {
            var id = Guid.NewGuid().ToString()[..8];
            project.Name = $"Debug Project {id}";
            project.Description = $"Auto-populated debug project {id} for testing on testnet. Created at {DateTime.Now:HH:mm:ss}.";
            project.Website = "https://angor.io";
            project.TargetAmount = 0.01m; // Minimal amount for testing
            project.PenaltyDays = 0;
            project.PenaltyThreshold = 0;
            project.StartDate = DateTime.Now.Date;
            project.FundingEndDate = DateTime.Now.Date;
            project.ExpiryDate = DateTime.Now.Date.AddDays(31);
            
            // Add default image URIs
            if (project is { AvatarUri: not null })
            {
                project.AvatarUri = GetDefaultImageUriString(170, 170);
            }
            
            if (project is { BannerUri: not null })
            {
                project.BannerUri = GetDefaultImageUriString(820, 312);
            }
        }
        catch (Exception ex)
        {
            // Silently fail - debug data population is best-effort
            Console.WriteLine($
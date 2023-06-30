using System;
using System.Collections.Generic;

/// <summary>
/// Encapsulates the public information related to an investment project.
/// This data, when combined with additional keys owned by an investor, facilitates the creation of an investment transaction.
/// </summary>
public class ProjectInvestmentInfo
{
    public string FounderKey { get; set; }
    
    public string AngorFeeKey { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public long ExpirationNumberOfBlocks { get; set; }
    public double TargetAmount { get; set; }
    public List<Seeders> Seeders { get; set; } = new();
    public List<Stage> Stages { get; set; } = new();
}
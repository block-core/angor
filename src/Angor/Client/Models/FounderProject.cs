using Angor.Shared.Models;

namespace Angor.Client.Models;

public class FounderProject : Project
{
    public int ProjectIndex { get; set; }
    public DateTime? LastRequestForSignaturesTime { get; set; }

    public string ProjectInfoEventId { get; set; }
    public bool NostrProfileCreated { get; set; }


    /// <summary>
    /// The total amount of the project that has been invested in,
    /// This parameter will only be set once the founder went to the spend page
    /// and was able to calaulate to total amount of funds that have been invested in the project.
    /// 
    /// The intention is to use this parameter to know if the founder should be forced to release
    /// the funds back to the investor by sending signature of a trx that spend coins to the investors address
    /// </summary>
    public decimal? TotalAvailableInvestedAmount { get; set; }

    public DateTime? ReleaseSignaturesTime { get; set; }

    public bool TargetInvestmentReached()
    {
        return TotalAvailableInvestedAmount >= ProjectInfo.TargetAmount;
    }

    public bool ProjectDateStarted()
    {
        return DateTime.UtcNow > ProjectInfo.StartDate;
    }

    public bool NostrMetadataCreated()
    {
        return !string.IsNullOrEmpty(Metadata?.Name);
    }

    public bool NostrApplicationSpecificDataCreated()
    {
        return !string.IsNullOrEmpty(ProjectInfoEventId);
    }
}
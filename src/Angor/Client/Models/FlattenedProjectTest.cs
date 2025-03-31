using Angor.Shared.Models;
using Blockcore.NBitcoin;

namespace Angor.Client.Models;

public class FlattenedProjectTest
{
    private const string displayName = "display_name";
    
    public string Name { get; set; }
    public string Website { get; set; }
    public string About { get; set; }
    public string Picture { get; set; }
    public string Nip05 { get; set; }
    public string Lud16 { get; set; }
    public string Banner { get; set; }
    public string Nip57 { get; set; }
    
    public int ProjectIndex { get; set; }
    public string FounderKey { get; set; }
    public string FounderRecoveryKey { get; set; }
    public string ProjectIdentifier { get; set; }
    
    public string NostrPubKey { get; set; }
    public DateTime StartDate { get; set; }
    public int PenaltyDays { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal TargetAmount { get; set; }
    public List<Stage> Stages { get; set; } = new();
    public string CreationTransactionId { get; set; }
    public ProjectSeeders ProjectSeeders { get; set; } = new();

    public ProjectMetadata GetMetadata()
    {
        return new ProjectMetadata
        {
            About = About, Banner = Banner, Lud16 = Lud16, Name = Name, Nip05 = Nip05, Nip57 = Nip57, Picture = Picture,
            Website = Website
        };
    }

    public void SetMetadata(ProjectMetadata? p)
    {
        if (p is null)
            return;
        About = p.About; Banner = p.Banner; Lud16 = p.Lud16; Name = p.Name; Nip05 = p.Nip05; Nip57 = p.Nip57;
        Picture = p.Picture; Website = p.Website;
    }
    
    public ProjectInfo GetProjectInfo()
    {
        return new ProjectInfo
        {
            //ProjectIndex = ProjectIndex,
            FounderKey = FounderKey,
            FounderRecoveryKey = FounderRecoveryKey,
            ProjectIdentifier = ProjectIdentifier,
            NostrPubKey = NostrPubKey,
            StartDate = StartDate,
            PenaltyDays = PenaltyDays,
            ExpiryDate = ExpiryDate,
            TargetAmount = Money.Satoshis(TargetAmount),
            Stages = new List<Stage>(Stages),
            //CreationTransactionId = CreationTransactionId,
            ProjectSeeders = ProjectSeeders
        };
    }
    
    public void SetProjectInfo(ProjectInfo info)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        //ProjectIndex = info.ProjectIndex;
        FounderKey = info.FounderKey;
        FounderRecoveryKey = info.FounderRecoveryKey;
        ProjectIdentifier = info.ProjectIdentifier;
        NostrPubKey = info.NostrPubKey;
        StartDate = info.StartDate;
        PenaltyDays = info.PenaltyDays;
        ExpiryDate = info.ExpiryDate;
        TargetAmount = info.TargetAmount;
        Stages = new List<Stage>(info.Stages);
        //CreationTransactionId = info.CreationTransactionId;
        ProjectSeeders = info.ProjectSeeders;
    }
}
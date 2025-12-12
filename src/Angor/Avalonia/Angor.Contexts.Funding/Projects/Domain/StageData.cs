namespace Angor.Contexts.Funding.Projects.Domain;

public class StageData
{
    public int StageIndex;
    public DateTime StageDate { get; set; }
    public IList<StageDataTrx> Items = new List<StageDataTrx>();
    public bool IsDynamic { get; set; }
    public long TotalAmount => Items.Sum(i => i.Amount);

    public Angor.Shared.Models.Stage Stage;
}
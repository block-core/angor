namespace Angor.Contexts.Funding.Projects.Domain;

public class StageData
{
    public int StageIndex;
    public Angor.Shared.Models.Stage Stage;
    public IList<StageDataTrx> Items = new List<StageDataTrx>();
}
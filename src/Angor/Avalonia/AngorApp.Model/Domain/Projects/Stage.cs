using AngorApp.Model.Domain.Amounts;

namespace AngorApp.Model.Domain.Projects;

public class Stage : IStage
{
    public DateTime ReleaseDate { get; set; }
    public long Amount { get; set; }
    public IAmountUI AmountUI => new AmountUI(Amount);
    public int Index { get; set; }
    public decimal RatioOfTotal { get; set; }
}

using AngorApp.Model.Amounts;

namespace AngorApp.Model.Projects;

public class StageDesign : IStage
{
    public DateTime ReleaseDate { get; set; } = DateTime.Now;
    public long Amount { get; set; } = 12345;
    public IAmountUI AmountUI => new AmountUI(Amount);
    public int Index { get; set; } = 1;
    public decimal RatioOfTotal { get; set; } = 1m;
}

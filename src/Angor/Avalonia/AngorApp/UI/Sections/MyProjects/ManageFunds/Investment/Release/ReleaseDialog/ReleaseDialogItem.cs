using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release.ReleaseDialog;

public class ReleaseDialogItem(ReleasableTransactionDto dto) : IReleaseDialogItem
{
    public IAmountUI Amount { get; } = new AmountUI(0);
    public string Address { get; } = dto.InvestmentEventId;
    public string InvestmentEventId { get; } = dto.InvestmentEventId;
    public bool IsSelected { get; set; } = dto.Released is null;
    public DateTime Arrived { get; } = dto.Arrived;
    public DateTime Approved { get; } = dto.Approved;
    public bool IsReleased { get; } = dto.Released is not null;
}

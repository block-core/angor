namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release.ReleaseDialog
{
    public interface IReleaseDialogItem
    {
        IAmountUI Amount { get; }
        string Address { get; }
        string InvestmentEventId { get; }
        bool IsSelected { get; set; }
    }
}
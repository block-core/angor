namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release.ReleaseDialog
{
    public class ReleaseDialogItemSample(IAmountUI amount, string address) : IReleaseDialogItem
    {
        public IAmountUI Amount { get; } = amount;
        public string Address { get; } = address;
        public bool IsSelected { get; set; } = true;
    }
}
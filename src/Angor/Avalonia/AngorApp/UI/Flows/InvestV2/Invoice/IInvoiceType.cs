namespace AngorApp.UI.Flows.InvestV2.Invoice
{
    public interface IInvoiceType
    {
        public string Name { get; }
        public string Address { get; set; }
    }
}
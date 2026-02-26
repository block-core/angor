namespace AngorApp.UI.Sections.Shared.ProjectV2
{
    public interface IPayment
    {
        int Id { get; }
        DateTimeOffset PaymentDate { get; }
        PaymentStatus Status { get; }
        IAmountUI Amount { get; }
    }
}
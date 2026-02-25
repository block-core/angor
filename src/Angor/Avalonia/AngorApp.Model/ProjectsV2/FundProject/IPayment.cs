namespace AngorApp.Model.ProjectsV2.FundProject
{
    public interface IPayment
    {
        int Id { get; }
        DateTimeOffset PaymentDate { get; }
        PaymentStatus Status { get; }
        IAmountUI Amount { get; }
    }
}
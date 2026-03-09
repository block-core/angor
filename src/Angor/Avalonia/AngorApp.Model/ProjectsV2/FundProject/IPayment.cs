namespace AngorApp.Model.ProjectsV2.FundProject
{
    public interface IPayment
    {
        int Id { get; }
        DateTimeOffset PaymentDate { get; }
        string Status { get; }
        IAmountUI Amount { get; }
    }
}
using Angor.Sdk.Funding.Projects.Dtos;

namespace AngorApp.Model.ProjectsV2.FundProject;

public class Payment(int id, DateTimeOffset paymentDate, IAmountUI amount, string status) : IPayment
{
    public int Id { get; } = id;
    public DateTimeOffset PaymentDate { get; } = paymentDate;
    public IAmountUI Amount { get; } = amount;
    public string Status { get; } = status;

    public static IReadOnlyCollection<IPayment> MapFrom(List<DynamicStageDto> dynamicStages)
    {
        return dynamicStages
            .OrderBy(d => d.ReleaseDate)
            .Select(d => (IPayment)new Payment(
                id: d.StageIndex,
                paymentDate: d.ReleaseDate,
                amount: new AmountUI(d.TotalAmount),
                status: d.Status
            )).ToList();
    }
}

namespace AngorApp.Sections.Founder.ManageFunds;

public class StageClaimViewModelDesign : ReactiveObject, IStageClaimViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; set; } =
    [
        new ClaimableStageDesign()
        {
            Transactions =
            [
                new ClaimableTransactionDesign
                {
                    Amount = new AmountUI(100000),
                    Address = "bc1qexampleaddress1",
                    ClaimStatus = ClaimStatus.Unspent
                },
                new ClaimableTransactionDesign
                {
                    Amount = new AmountUI(200000),
                    Address = "bc1qexampleaddress2",
                    ClaimStatus = ClaimStatus.Unspent
                },
                new ClaimableTransactionDesign
                {
                    Amount = new AmountUI(150000),
                    Address = "bc1qexampleaddress3",
                    ClaimStatus = ClaimStatus.Pending
                },
                new ClaimableTransactionDesign
                {
                    Amount = new AmountUI(50000),
                    Address = "bc1qexampleaddress4",
                    ClaimStatus = ClaimStatus.Unspent
                },
                new ClaimableTransactionDesign
                {
                    Amount = new AmountUI(30000),
                    Address = "bc1qexampleaddress5",
                    ClaimStatus = ClaimStatus.SpentByFounder
                }
            ],
        }
    ];

    public DateTime EstimatedCompletion { get; set; } = DateTime.Now.AddDays(30);
}
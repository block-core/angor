namespace Angor.Shared.Models;

/// <summary>
/// Contains all the requisite information for an investor to formulate an investment transaction.
/// This data is unique and tailored to each individual investor.
/// </summary>
public class InvestorContext
{
    public string InvestorKey { get; set; } = string.Empty;

    public string InvestorSecretHash { get; set; } = string.Empty;

    public ProjectInfo ProjectInfo { get; set; } = null!;

    public string TransactionHex { get; set; } = string.Empty;


    // todo: does this info need to be in this class?
    // ==============================================

    public string ChangeAddress { get; set; } = string.Empty;
}
namespace Angor.Contexts.Funding.Investor.Requests.CreateInvestment;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; }
    public string TransactionHex { get; set; }
}
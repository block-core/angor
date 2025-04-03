namespace Angor.Contexts.Funding.Investment.Commands.CreateInvestment;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; }
    public string TransactionHex { get; set; }
}
namespace Angor.Contexts.Projects.Infrastructure.Impl.Commands;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; }
    public string TransactionHex { get; set; }
}
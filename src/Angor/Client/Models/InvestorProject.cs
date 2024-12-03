using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Client.Models;

public class InvestorProject : Project
{
    public string TransactionId { get; set; }
    public string RecoveryTransactionId { get; set; }
    public string RecoveryReleaseTransactionId { get; set; }
    public string EndOfProjectTransactionId { get; set; }
    public SignatureInfo? SignaturesInfo { get; set; } = new();
    public string? SignedTransactionHex { get; set; }
    
    public long? AmountInvested { get; set; }
    public long? AmountInRecovery { get; set; }

    public string InvestorPublicKey { get; set; }
    public string InvestorNPub { get; set; }

    public bool WaitingForFounderResponse()
    {
        return ReceivedFounderSignatures() == false && SignaturesInfo?.TimeOfSignatureRequest != null;
    }

    public bool InvestedInProject()
    {
        return !string.IsNullOrEmpty(TransactionId);
    }

    public bool ReceivedFounderSignatures()
    {
        return SignaturesInfo?.Signatures.Any() ?? false;
    }

    public void CompleteProjectInvestment(Transaction investmentTransaction)
    {
        TransactionId = investmentTransaction.GetHash().ToString();
        SignedTransactionHex = null;
        AmountInvested = investmentTransaction.Outputs.Skip(2).Take(ProjectInfo.Stages.Count).Sum(s => s.Value);
    }
}
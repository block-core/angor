using Angor.Shared.Models;
using NBitcoin;

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

    /// <summary>
    /// The address to release the funds to if the project did not reach the target.
    /// This will be used by the founder when signing the release outputs
    /// </summary>
    public string UnfundedReleaseAddress { get; set; }

    /// <summary>
    /// The trxid of an unfunded project that the investor has released the funds without a penalty 
    /// </summary>
    public string UnfundedReleaseTransactionId { get; set; }

    public string AdditionalNpub { get; set; }

    public DateTime? LastRequestForMessagesTime { get; set; }

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

        // Fund/Subscribe projects have no fixed stages (stage count comes from the dynamic
        // pattern), so counting via ProjectInfo.Stages would yield 0. In that case sum all
        // taproot stage outputs instead (skip Angor fee + OP_RETURN, exclude change).
        if (ProjectInfo.Stages.Count > 0)
        {
            AmountInvested = investmentTransaction.Outputs.Skip(2).Take(ProjectInfo.Stages.Count).Sum(s => s.Value);
        }
        else if (AmountInvested is null or 0)
        {
            AmountInvested = investmentTransaction.Outputs.Skip(2)
                .Where(o => o.ScriptPubKey.IsScriptType(ScriptType.Taproot))
                .Sum(s => s.Value);
        }
    }

}
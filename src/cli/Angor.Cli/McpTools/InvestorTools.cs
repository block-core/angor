using System.ComponentModel;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using ModelContextProtocol.Server;

namespace Angor.Cli.McpTools;

[McpServerToolType]
public class InvestorTools(IInvestmentAppService investorService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description("Build an investment transaction draft for a project")]
    public async Task<string> InvestorBuildDraft(string walletId, string projectId, long amountSats, long feeRateSatPerVb)
    {
        var result = await investorService.BuildInvestmentDraft(
            new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                new WalletId(walletId), new ProjectId(projectId), new Amount(amountSats), new DomainFeerate(feeRateSatPerVb)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("List all investments made by this wallet")]
    public async Task<string> InvestorMyInvestments(string walletId)
    {
        var result = await investorService.GetInvestments(
            new GetInvestments.GetInvestmentsRequest(new WalletId(walletId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get total amount invested across all projects in satoshis")]
    public async Task<string> InvestorTotalInvested(string walletId)
    {
        var result = await investorService.GetTotalInvested(
            new GetTotalInvested.GetTotalInvestedRequest(new WalletId(walletId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("List penalties across all investments")]
    public async Task<string> InvestorPenalties(string walletId)
    {
        var result = await investorService.GetPenalties(
            new GetPenalties.GetPenaltiesRequest(new WalletId(walletId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Check if an investment amount is above the penalty threshold for a project")]
    public async Task<string> InvestorPenaltyCheck(string projectId, long amountSats)
    {
        var result = await investorService.IsInvestmentAbovePenaltyThreshold(
            new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(new ProjectId(projectId), new Amount(amountSats)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get recovery status for an investment in a project")]
    public async Task<string> InvestorRecoveryStatus(string walletId, string projectId)
    {
        var result = await investorService.GetRecoveryStatus(
            new GetRecoveryStatus.GetRecoveryStatusRequest(new WalletId(walletId), new ProjectId(projectId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Build a recovery transaction to reclaim invested funds")]
    public async Task<string> InvestorBuildRecovery(string walletId, string projectId, long feeRateSatPerVb)
    {
        var result = await investorService.BuildRecoveryTransaction(
            new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(
                new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRateSatPerVb)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Build an unfunded release transaction")]
    public async Task<string> InvestorBuildRelease(string walletId, string projectId, long feeRateSatPerVb)
    {
        var result = await investorService.BuildUnfundedReleaseTransaction(
            new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(
                new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRateSatPerVb)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Build a penalty release transaction")]
    public async Task<string> InvestorBuildPenaltyRelease(string walletId, string projectId, long feeRateSatPerVb)
    {
        var result = await investorService.BuildPenaltyReleaseTransaction(
            new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(
                new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRateSatPerVb)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Build an end-of-project claim transaction")]
    public async Task<string> InvestorBuildEopClaim(string walletId, string projectId, long feeRateSatPerVb)
    {
        var result = await investorService.BuildEndOfProjectClaim(
            new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(
                new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRateSatPerVb)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Check if release signatures are available for an investment")]
    public async Task<string> InvestorCheckSignatures(string walletId, string projectId)
    {
        var result = await investorService.CheckForReleaseSignatures(
            new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(new WalletId(walletId), new ProjectId(projectId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Submit a signed investor transaction to the network")]
    public async Task<string> InvestorSubmitTx(string signedTxHex, string transactionId, long feeSats,
        string? walletId = null, string? projectId = null)
    {
        var draft = new TransactionDraft
        {
            SignedTxHex = signedTxHex,
            TransactionId = transactionId,
            TransactionFee = new Amount(feeSats)
        };

        var result = await investorService.SubmitTransactionFromDraft(
            new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                walletId, projectId != null ? new ProjectId(projectId) : null, draft));
        return result.IsSuccess
            ? JsonSerializer.Serialize(new { transactionId = result.Value.TransactionId }, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get investor Nostr secret key (nsec) for a project")]
    public async Task<string> InvestorGetNsec(string walletId, string founderKey)
    {
        var result = await investorService.GetInvestorNsec(
            new GetInvestorNsec.GetInvestorNsecRequest(new WalletId(walletId), founderKey));
        return result.IsSuccess
            ? JsonSerializer.Serialize(new { nsec = result.Value.Nsec }, JsonOptions)
            : $"Error: {result.Error}";
    }
}

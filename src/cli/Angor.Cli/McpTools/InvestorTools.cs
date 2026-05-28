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

    [McpServerTool, Description("Build an investment transaction draft for a project. For Fund/Subscribe projects, patternId is required (byte 0-255).")]
    public async Task<string> InvestorBuildDraft(string walletId, string projectId, long amountSats, long feeRateSatPerVb, byte? patternId = null)
    {
        var result = await investorService.BuildInvestmentDraft(
            new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                new WalletId(walletId), new ProjectId(projectId), new Amount(amountSats), new DomainFeerate(feeRateSatPerVb),
                PatternId: patternId));
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

    [McpServerTool, Description("Get recovery status for an investment in a project. Returns current state and available actions the investor can take to reclaim funds.")]
    public async Task<string> InvestorRecoveryStatus(string walletId, string projectId)
    {
        var result = await investorService.GetRecoveryStatus(
            new GetRecoveryStatus.GetRecoveryStatusRequest(new WalletId(walletId), new ProjectId(projectId)));
        if (result.IsFailure)
            return $"Error: {result.Error}";

        var dto = result.Value.RecoveryData;
        var actions = ComputeAvailableActions(dto);

        return JsonSerializer.Serialize(new
        {
            recoveryData = result.Value,
            availableActions = actions
        }, JsonOptions);
    }

    private static List<object> ComputeAvailableActions(Angor.Sdk.Funding.Investor.Dtos.InvestorProjectRecoveryDto dto)
    {
        var actions = new List<object>();

        if (!dto.HasUnspentItems)
            return actions;

        if (dto.EndOfProject)
        {
            actions.Add(new
            {
                action = "end-of-project-claim",
                description = "Project has expired. Claim all remaining unspent funds back to your wallet (no penalty).",
                command = "investor build-eop-claim"
            });
        }

        if (dto.HasReleaseSignatures)
        {
            actions.Add(new
            {
                action = "release-claim",
                description = "Founder has released your funds. Build a release transaction to claim them without penalty.",
                command = "investor build-release"
            });
        }

        if (!dto.EndOfProject && dto.IsAboveThreshold)
        {
            actions.Add(new
            {
                action = "recovery",
                description = $"Initiate early recovery of your investment. This triggers a penalty period of {dto.PenaltyDays} days before funds can be fully claimed.",
                command = "investor build-recovery"
            });
        }

        if (dto.HasSpendableItemsInPenalty)
        {
            actions.Add(new
            {
                action = "penalty-release",
                description = "Penalty period has expired. Claim your recovered funds.",
                command = "investor build-penalty-release"
            });
        }

        if (actions.Count == 0)
        {
            actions.Add(new
            {
                action = "wait",
                description = $"This is a direct investment (below threshold). Early recovery is not available. Funds can be claimed after the project expires on {dto.ExpiryDate:yyyy-MM-dd}, or if the founder releases them.",
                command = ""
            });
        }

        return actions;
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

    [McpServerTool, Description("Submit a signed investor transaction to the network. For investment transactions, provide investorKey and amountSats to enable Nostr founder notification.")]
    public async Task<string> InvestorSubmitTx(string signedTxHex, string transactionId, long feeSats,
        string? walletId = null, string? projectId = null, string? investorKey = null, long? amountSats = null)
    {
        TransactionDraft draft;
        if (!string.IsNullOrEmpty(investorKey))
        {
            draft = new Angor.Sdk.Funding.Shared.TransactionDrafts.InvestmentDraft(investorKey)
            {
                SignedTxHex = signedTxHex,
                TransactionId = transactionId,
                TransactionFee = new Amount(feeSats),
                InvestedAmount = new Amount(amountSats ?? 0)
            };
        }
        else
        {
            draft = new TransactionDraft
            {
                SignedTxHex = signedTxHex,
                TransactionId = transactionId,
                TransactionFee = new Amount(feeSats)
            };
        }

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

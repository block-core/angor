using System.ComponentModel;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using ModelContextProtocol.Server;

namespace Angor.Cli.McpTools;

[McpServerToolType]
public class FounderTools(IFounderAppService founderService, IProjectAppService projectService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description("Generate project keys for a new project. Returns founder key, recovery key, nostr pubkey, and project identifier.")]
    public async Task<string> FounderCreateKeys(string walletId)
    {
        var result = await founderService.CreateProjectKeys(
            new CreateProjectKeys.CreateProjectKeysRequest(new WalletId(walletId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("List projects created by this wallet")]
    public async Task<string> FounderMyProjects(string walletId)
    {
        var result = await projectService.GetFounderProjects(new WalletId(walletId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Scan the network for projects created by this wallet")]
    public async Task<string> FounderScanProjects(string walletId)
    {
        var result = await projectService.ScanFounderProjects(new WalletId(walletId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("List investments in a project. Shows event ID, amount, status, and date for each investment.")]
    public async Task<string> FounderInvestments(string walletId, string projectId)
    {
        var result = await founderService.GetProjectInvestments(
            new GetProjectInvestments.GetProjectInvestmentsRequest(new WalletId(walletId), new ProjectId(projectId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Approve a pending investment request")]
    public async Task<string> FounderApprove(string walletId, string projectId, string eventId,
        string investorPubKey, string txHex, long amountSats)
    {
        var investment = new Investment(eventId, DateTime.UtcNow, txHex, investorPubKey, amountSats, InvestmentStatus.PendingFounderSignatures);
        var result = await founderService.ApproveInvestment(
            new ApproveInvestment.ApproveInvestmentRequest(new WalletId(walletId), new ProjectId(projectId), investment));
        return result.IsSuccess
            ? "Investment approved."
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("List claimable (spendable) transactions for a project")]
    public async Task<string> FounderClaimable(string walletId, string projectId)
    {
        var result = await founderService.GetClaimableTransactions(
            new GetClaimableTransactions.GetClaimableTransactionsRequest(new WalletId(walletId), new ProjectId(projectId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("List releasable transactions for a project")]
    public async Task<string> FounderReleasable(string walletId, string projectId)
    {
        var result = await founderService.GetReleasableTransactions(
            new GetReleasableTransactions.GetReleasableTransactionsRequest(new WalletId(walletId), new ProjectId(projectId)));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Release funds for approved investments. Provide a comma-separated list of investment event IDs.")]
    public async Task<string> FounderRelease(string walletId, string projectId, string eventIdsCsv)
    {
        var eventIds = eventIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await founderService.ReleaseFunds(
            new ReleaseFunds.ReleaseFundsRequest(new WalletId(walletId), new ProjectId(projectId), eventIds));
        return result.IsSuccess
            ? "Funds released."
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Spend funds from a project stage. Creates a transaction draft. For Fund/Subscribe projects, investmentStageIndex is the 0-based stage index within the investment transaction (defaults to stageId).")]
    public async Task<string> FounderSpendStage(string walletId, string projectId, long feeRateSatPerVb,
        int stageId, string investorAddress, int? investmentStageIndex = null)
    {
        var fee = new FeeEstimation { FeeRate = feeRateSatPerVb * 1000, Confirmations = 1 };
        var toSpend = new[] { new SpendTransactionDto { InvestorAddress = investorAddress, StageId = stageId, InvestmentStageIndex = investmentStageIndex ?? stageId } };

        var result = await founderService.SpendStageFunds(
            new SpendStageFunds.SpendStageFundsRequest(new WalletId(walletId), new ProjectId(projectId), fee, toSpend));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Submit a signed founder transaction to the network")]
    public async Task<string> FounderSubmitTx(string signedTxHex, string transactionId, long feeSats)
    {
        var draft = new TransactionDraft
        {
            SignedTxHex = signedTxHex,
            TransactionId = transactionId,
            TransactionFee = new Amount(feeSats)
        };

        var result = await founderService.SubmitTransactionFromDraft(
            new PublishFounderTransaction.PublishFounderTransactionRequest(draft));
        return result.IsSuccess
            ? JsonSerializer.Serialize(new { transactionId = result.Value.TransactionId }, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get moonshot project data by event ID")]
    public async Task<string> FounderMoonshot(string eventId)
    {
        var result = await founderService.GetMoonshotProject(
            new GetMoonshotProject.GetMoonshotProjectRequest(eventId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Create a Nostr profile for a new project. Provide project data as JSON string with fields: projectName, description, avatarUri, bannerUri, sats, startDate, targetAmount, penaltyDays, stages (array of {startDate, percentageOfTotal}), projectType (0=Invest, 1=Fund). Returns the profile event ID.")]
    public async Task<string> FounderCreateProfile(string walletId, string founderKey, string recoveryKey,
        string nostrPubKey, string projectIdentifier, string projectDataJson)
    {
        var project = JsonSerializer.Deserialize<CreateProjectDto>(projectDataJson, JsonOptions);
        if (project is null)
            return "Error: Failed to deserialize project data JSON.";

        var seed = new ProjectSeedDto(founderKey, recoveryKey, nostrPubKey, projectIdentifier);
        var result = await projectService.CreateProjectProfile(new WalletId(walletId), seed, project);
        return result.IsSuccess
            ? JsonSerializer.Serialize(new { eventId = result.Value.EventId }, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Publish project info (stages, metadata) to Nostr. Use the same project data JSON as create-profile. Returns the info event ID needed for create-project.")]
    public async Task<string> FounderCreateInfo(string walletId, string founderKey, string recoveryKey,
        string nostrPubKey, string projectIdentifier, string projectDataJson)
    {
        var project = JsonSerializer.Deserialize<CreateProjectDto>(projectDataJson, JsonOptions);
        if (project is null)
            return "Error: Failed to deserialize project data JSON.";

        var seed = new ProjectSeedDto(founderKey, recoveryKey, nostrPubKey, projectIdentifier);
        var result = await projectService.CreateProjectInfo(new WalletId(walletId), project, seed);
        return result.IsSuccess
            ? JsonSerializer.Serialize(new { eventId = result.Value.EventId }, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Create the on-chain Bitcoin project transaction. Requires the info event ID from FounderCreateInfo. Use the same project data JSON. Returns a transaction draft with txId and fee.")]
    public async Task<string> FounderCreateProject(string walletId, long feeSats, string infoEventId,
        string founderKey, string recoveryKey, string nostrPubKey, string projectIdentifier, string projectDataJson)
    {
        var project = JsonSerializer.Deserialize<CreateProjectDto>(projectDataJson, JsonOptions);
        if (project is null)
            return "Error: Failed to deserialize project data JSON.";

        var seed = new ProjectSeedDto(founderKey, recoveryKey, nostrPubKey, projectIdentifier);
        var result = await projectService.CreateProject(new WalletId(walletId), feeSats, project, infoEventId, seed);
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }
}

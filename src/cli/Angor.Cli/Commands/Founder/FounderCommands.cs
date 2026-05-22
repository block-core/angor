using System.CommandLine;
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
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Cli.Commands.Founder;

public static class FounderCommands
{
    public static Command Build(IServiceProvider services, JsonSerializerOptions jsonOptions)
    {
        var founderService = services.GetRequiredService<IFounderAppService>();
        var projectService = services.GetRequiredService<IProjectAppService>();

        var cmd = new Command("founder", "Founder project management commands");

        cmd.AddCommand(BuildCreateKeysCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildMyProjectsCommand(projectService, jsonOptions));
        cmd.AddCommand(BuildScanProjectsCommand(projectService, jsonOptions));
        cmd.AddCommand(BuildInvestmentsCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildApproveCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildClaimableCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildReleasableCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildReleaseCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildSpendStageCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildSubmitTxCommand(founderService, jsonOptions));
        cmd.AddCommand(BuildMoonshotCommand(founderService, jsonOptions));

        return cmd;
    }

    private static Command BuildCreateKeysCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };

        var cmd = new Command("create-keys", "Generate project keys for a new project") { walletIdOption };
        cmd.SetHandler(async (string walletId) =>
        {
            var result = await founderService.CreateProjectKeys(
                new CreateProjectKeys.CreateProjectKeysRequest(new WalletId(walletId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            var seed = result.Value.ProjectSeedDto;
            Console.WriteLine($"Founder Key:      {seed.FounderKey}");
            Console.WriteLine($"Recovery Key:     {seed.FounderRecoveryKey}");
            Console.WriteLine($"Nostr PubKey:     {seed.NostrPubKey}");
            Console.WriteLine($"Project ID:       {seed.ProjectIdentifier}");
        }, walletIdOption);
        return cmd;
    }

    private static Command BuildMyProjectsCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("my-projects", "List projects created by this wallet") { walletIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, bool json) =>
        {
            var result = await projectService.GetFounderProjects(new WalletId(walletId));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
                return;
            }

            foreach (var project in result.Value.Projects)
            {
                Console.WriteLine($"  {project.Id}  {project.Name ?? "(no name)"}");
            }
        }, walletIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildScanProjectsCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("scan-projects", "Scan the network for projects created by this wallet") { walletIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, bool json) =>
        {
            var result = await projectService.ScanFounderProjects(new WalletId(walletId));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
                return;
            }

            foreach (var project in result.Value.Projects)
            {
                Console.WriteLine($"  {project.Id}  {project.Name ?? "(no name)"}");
            }
        }, walletIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildInvestmentsCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("investments", "List investments in a project") { walletIdOption, projectIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, string projectId, bool json) =>
        {
            var result = await founderService.GetProjectInvestments(
                new GetProjectInvestments.GetProjectInvestmentsRequest(new WalletId(walletId), new ProjectId(projectId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
                return;
            }

            foreach (var inv in result.Value.Investments)
            {
                Console.WriteLine($"  {inv.EventId}  {inv.Amount} sats  {inv.Status}  {inv.CreatedOn:yyyy-MM-dd}");
            }
        }, walletIdOption, projectIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildApproveCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var eventIdOption = new Option<string>("--event-id", "Investment event ID to approve") { IsRequired = true };
        var investorPubKeyOption = new Option<string>("--investor-pubkey", "Investor Nostr public key") { IsRequired = true };
        var txHexOption = new Option<string>("--tx-hex", "Investment transaction hex") { IsRequired = true };
        var amountOption = new Option<long>("--amount", "Investment amount in sats") { IsRequired = true };

        var cmd = new Command("approve", "Approve a pending investment")
        {
            walletIdOption, projectIdOption, eventIdOption, investorPubKeyOption, txHexOption, amountOption
        };
        cmd.SetHandler(async (string walletId, string projectId, string eventId, string investorPubKey, string txHex, long amount) =>
        {
            var investment = new Investment(eventId, DateTime.UtcNow, txHex, investorPubKey, amount, InvestmentStatus.PendingFounderSignatures);
            var result = await founderService.ApproveInvestment(
                new ApproveInvestment.ApproveInvestmentRequest(new WalletId(walletId), new ProjectId(projectId), investment));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine("Investment approved.");
        }, walletIdOption, projectIdOption, eventIdOption, investorPubKeyOption, txHexOption, amountOption);
        return cmd;
    }

    private static Command BuildClaimableCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("claimable", "List claimable transactions for a project") { walletIdOption, projectIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, string projectId, bool json) =>
        {
            var result = await founderService.GetClaimableTransactions(
                new GetClaimableTransactions.GetClaimableTransactionsRequest(new WalletId(walletId), new ProjectId(projectId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildReleasableCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("releasable", "List releasable transactions for a project") { walletIdOption, projectIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, string projectId, bool json) =>
        {
            var result = await founderService.GetReleasableTransactions(
                new GetReleasableTransactions.GetReleasableTransactionsRequest(new WalletId(walletId), new ProjectId(projectId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildReleaseCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var eventIdsOption = new Option<string[]>("--event-ids", "Investment event IDs to release (space-separated)") { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        var cmd = new Command("release", "Release funds for approved investments") { walletIdOption, projectIdOption, eventIdsOption };
        cmd.SetHandler(async (string walletId, string projectId, string[] eventIds) =>
        {
            var result = await founderService.ReleaseFunds(
                new ReleaseFunds.ReleaseFundsRequest(new WalletId(walletId), new ProjectId(projectId), eventIds));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine("Funds released.");
        }, walletIdOption, projectIdOption, eventIdsOption);
        return cmd;
    }

    private static Command BuildSpendStageCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };
        var stageIdOption = new Option<int>("--stage-id", "Stage ID to spend") { IsRequired = true };
        var addressOption = new Option<string>("--address", "Investor address") { IsRequired = true };

        var cmd = new Command("spend-stage", "Spend funds from a project stage")
        {
            walletIdOption, projectIdOption, feeRateOption, stageIdOption, addressOption
        };
        cmd.SetHandler(async (string walletId, string projectId, long feeRate, int stageId, string address) =>
        {
            var fee = new FeeEstimation { FeeRate = feeRate * 1000, Confirmations = 1 };
            var toSpend = new[] { new SpendTransactionDto { InvestorAddress = address, StageId = stageId } };

            var result = await founderService.SpendStageFunds(
                new SpendStageFunds.SpendStageFundsRequest(new WalletId(walletId), new ProjectId(projectId), fee, toSpend));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Transaction draft created: {result.Value.TransactionDraft.TransactionId}");
            Console.WriteLine($"Fee: {result.Value.TransactionDraft.TransactionFee} sats");
        }, walletIdOption, projectIdOption, feeRateOption, stageIdOption, addressOption);
        return cmd;
    }

    private static Command BuildSubmitTxCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var txHexOption = new Option<string>("--tx-hex", "Signed transaction hex") { IsRequired = true };
        var txIdOption = new Option<string>("--tx-id", "Transaction ID") { IsRequired = true };
        var feeOption = new Option<long>("--fee", "Transaction fee in sats") { IsRequired = true };

        var cmd = new Command("submit-tx", "Submit a signed founder transaction") { txHexOption, txIdOption, feeOption };
        cmd.SetHandler(async (string txHex, string txId, long fee) =>
        {
            var draft = new Angor.Sdk.Funding.Shared.TransactionDraft
            {
                SignedTxHex = txHex,
                TransactionId = txId,
                TransactionFee = new Amount(fee)
            };

            var result = await founderService.SubmitTransactionFromDraft(
                new PublishFounderTransaction.PublishFounderTransactionRequest(draft));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Transaction published: {result.Value.TransactionId}");
        }, txHexOption, txIdOption, feeOption);
        return cmd;
    }

    private static Command BuildMoonshotCommand(IFounderAppService founderService, JsonSerializerOptions jsonOptions)
    {
        var eventIdOption = new Option<string>("--event-id", "Moonshot event ID") { IsRequired = true };

        var cmd = new Command("moonshot", "Get moonshot project data") { eventIdOption };
        cmd.SetHandler(async (string eventId) =>
        {
            var result = await founderService.GetMoonshotProject(
                new GetMoonshotProject.GetMoonshotProjectRequest(eventId));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, eventIdOption);
        return cmd;
    }
}

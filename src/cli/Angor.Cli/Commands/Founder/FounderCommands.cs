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
using Angor.Shared;
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
        cmd.AddCommand(BuildCreateProfileCommand(projectService, jsonOptions));
        cmd.AddCommand(BuildCreateInfoCommand(projectService, jsonOptions));
        cmd.AddCommand(BuildCreateProjectCommand(projectService, jsonOptions));

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
        var addressOption = new Option<string[]>("--address", "Investor address(es)") { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        var cmd = new Command("spend-stage", "Spend funds from a project stage")
        {
            walletIdOption, projectIdOption, feeRateOption, stageIdOption, addressOption
        };
        cmd.SetHandler(async (string walletId, string projectId, long feeRate, int stageId, string[] addresses) =>
        {
            var fee = new FeeEstimation { FeeRate = feeRate * 1000, Confirmations = 1 };
            var toSpend = addresses.Select(a => new SpendTransactionDto { InvestorAddress = a, StageId = stageId }).ToArray();

            var result = await founderService.SpendStageFunds(
                new SpendStageFunds.SpendStageFundsRequest(new WalletId(walletId), new ProjectId(projectId), fee, toSpend));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Transaction draft created: {result.Value.TransactionDraft.TransactionId}");
            Console.WriteLine($"Fee: {result.Value.TransactionDraft.TransactionFee} sats");
            Console.WriteLine($"Transaction Hex: {result.Value.TransactionDraft.SignedTxHex}");
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

    private static Command BuildCreateProfileCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var inputFileOption = new Option<string>("--input-file", "Path to JSON file with project data (CreateProjectDto)") { IsRequired = true };
        var founderKeyOption = new Option<string>("--founder-key", "Founder key from create-keys") { IsRequired = true };
        var recoveryKeyOption = new Option<string>("--recovery-key", "Recovery key from create-keys") { IsRequired = true };
        var nostrPubKeyOption = new Option<string>("--nostr-pubkey", "Nostr public key from create-keys") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project identifier from create-keys") { IsRequired = true };

        var cmd = new Command("create-profile", "Create the Nostr profile for a new project")
        {
            walletIdOption, inputFileOption, founderKeyOption, recoveryKeyOption, nostrPubKeyOption, projectIdOption
        };
        cmd.SetHandler(async (string walletId, string inputFile, string founderKey, string recoveryKey, string nostrPubKey, string projectId) =>
        {
            var json = await File.ReadAllTextAsync(inputFile);
            var project = JsonSerializer.Deserialize<CreateProjectDto>(json, jsonOptions);
            if (project is null)
            {
                Console.Error.WriteLine("Error: Failed to deserialize project data from input file.");
                return;
            }

            var seed = new ProjectSeedDto(founderKey, recoveryKey, nostrPubKey, projectId);
            var result = await projectService.CreateProjectProfile(new WalletId(walletId), seed, project);

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Profile created. Event ID: {result.Value.EventId}");
        }, walletIdOption, inputFileOption, founderKeyOption, recoveryKeyOption, nostrPubKeyOption, projectIdOption);
        return cmd;
    }

    private static Command BuildCreateInfoCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var inputFileOption = new Option<string>("--input-file", "Path to JSON file with project data (CreateProjectDto)") { IsRequired = true };
        var founderKeyOption = new Option<string>("--founder-key", "Founder key") { IsRequired = true };
        var recoveryKeyOption = new Option<string>("--recovery-key", "Recovery key") { IsRequired = true };
        var nostrPubKeyOption = new Option<string>("--nostr-pubkey", "Nostr public key") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project identifier") { IsRequired = true };

        var cmd = new Command("create-info", "Publish project info (stages, metadata) to Nostr")
        {
            walletIdOption, inputFileOption, founderKeyOption, recoveryKeyOption, nostrPubKeyOption, projectIdOption
        };
        cmd.SetHandler(async (string walletId, string inputFile, string founderKey, string recoveryKey, string nostrPubKey, string projectId) =>
        {
            var json = await File.ReadAllTextAsync(inputFile);
            var project = JsonSerializer.Deserialize<CreateProjectDto>(json, jsonOptions);
            if (project is null)
            {
                Console.Error.WriteLine("Error: Failed to deserialize project data from input file.");
                return;
            }

            var seed = new ProjectSeedDto(founderKey, recoveryKey, nostrPubKey, projectId);
            var result = await projectService.CreateProjectInfo(new WalletId(walletId), project, seed);

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Project info published. Event ID: {result.Value.EventId}");
        }, walletIdOption, inputFileOption, founderKeyOption, recoveryKeyOption, nostrPubKeyOption, projectIdOption);
        return cmd;
    }

    private static Command BuildCreateProjectCommand(IProjectAppService projectService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var inputFileOption = new Option<string>("--input-file", "Path to JSON file with project data (CreateProjectDto)") { IsRequired = true };
        var feeOption = new Option<long>("--fee", "Selected fee in sats") { IsRequired = true };
        var infoEventIdOption = new Option<string>("--info-event-id", "Project info event ID from create-info") { IsRequired = true };
        var founderKeyOption = new Option<string>("--founder-key", "Founder key") { IsRequired = true };
        var recoveryKeyOption = new Option<string>("--recovery-key", "Recovery key") { IsRequired = true };
        var nostrPubKeyOption = new Option<string>("--nostr-pubkey", "Nostr public key") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project identifier") { IsRequired = true };

        var cmd = new Command("create-project", "Create the on-chain Bitcoin project transaction")
        {
            walletIdOption, inputFileOption, feeOption, infoEventIdOption,
            founderKeyOption, recoveryKeyOption, nostrPubKeyOption, projectIdOption
        };
        cmd.SetHandler(async context =>
        {
            var walletId = context.ParseResult.GetValueForOption(walletIdOption)!;
            var inputFile = context.ParseResult.GetValueForOption(inputFileOption)!;
            var fee = context.ParseResult.GetValueForOption(feeOption);
            var infoEventId = context.ParseResult.GetValueForOption(infoEventIdOption)!;
            var founderKey = context.ParseResult.GetValueForOption(founderKeyOption)!;
            var recoveryKey = context.ParseResult.GetValueForOption(recoveryKeyOption)!;
            var nostrPubKey = context.ParseResult.GetValueForOption(nostrPubKeyOption)!;
            var projectId = context.ParseResult.GetValueForOption(projectIdOption)!;

            var json = await File.ReadAllTextAsync(inputFile);
            var project = JsonSerializer.Deserialize<CreateProjectDto>(json, jsonOptions);
            if (project is null)
            {
                Console.Error.WriteLine("Error: Failed to deserialize project data from input file.");
                return;
            }

            var seed = new ProjectSeedDto(founderKey, recoveryKey, nostrPubKey, projectId);
            var result = await projectService.CreateProject(new WalletId(walletId), fee, project, infoEventId, seed);

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Project created on-chain.");
            Console.WriteLine($"Transaction ID: {result.Value.TransactionDraft.TransactionId}");
            Console.WriteLine($"Transaction Hex: {result.Value.TransactionDraft.SignedTxHex}");
            Console.WriteLine($"Fee: {result.Value.TransactionDraft.TransactionFee} sats");
        });
        return cmd;
    }
}

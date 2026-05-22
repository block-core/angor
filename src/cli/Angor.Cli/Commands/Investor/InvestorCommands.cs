using System.CommandLine;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Cli.Commands.Investor;

public static class InvestorCommands
{
    public static Command Build(IServiceProvider services, JsonSerializerOptions jsonOptions)
    {
        var investorService = services.GetRequiredService<IInvestmentAppService>();

        var cmd = new Command("investor", "Investor commands");

        cmd.AddCommand(BuildDraftCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildMyInvestmentsCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildTotalInvestedCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildPenaltiesCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildPenaltyCheckCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildRecoveryStatusCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildBuildRecoveryCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildBuildReleaseCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildBuildPenaltyReleaseCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildBuildEopClaimCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildCheckSignaturesCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildSubmitTxCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildGetNsecCommand(investorService, jsonOptions));

        return cmd;
    }

    private static Command BuildDraftCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var amountOption = new Option<long>("--amount", "Amount to invest in sats") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };

        var cmd = new Command("build-draft", "Build an investment transaction draft") { walletIdOption, projectIdOption, amountOption, feeRateOption };
        cmd.SetHandler(async (string walletId, string projectId, long amount, long feeRate) =>
        {
            var result = await investorService.BuildInvestmentDraft(
                new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                    new WalletId(walletId), new ProjectId(projectId), new Amount(amount), new DomainFeerate(feeRate)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption, amountOption, feeRateOption);
        return cmd;
    }

    private static Command BuildMyInvestmentsCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("my-investments", "List all investments made by this wallet") { walletIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, bool json) =>
        {
            var result = await investorService.GetInvestments(
                new GetInvestments.GetInvestmentsRequest(new WalletId(walletId)));

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
                Console.WriteLine($"  {project.Id}  {project.Name}");
            }
        }, walletIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildTotalInvestedCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };

        var cmd = new Command("total-invested", "Get total amount invested across all projects") { walletIdOption };
        cmd.SetHandler(async (string walletId) =>
        {
            var result = await investorService.GetTotalInvested(
                new GetTotalInvested.GetTotalInvestedRequest(new WalletId(walletId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Total invested: {result.Value.TotalInvestedSats} sats");
        }, walletIdOption);
        return cmd;
    }

    private static Command BuildPenaltiesCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("penalties", "List penalties across all investments") { walletIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, bool json) =>
        {
            var result = await investorService.GetPenalties(
                new GetPenalties.GetPenaltiesRequest(new WalletId(walletId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildPenaltyCheckCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var amountOption = new Option<long>("--amount", "Amount in sats to check") { IsRequired = true };

        var cmd = new Command("penalty-check", "Check if an investment amount is above the penalty threshold") { projectIdOption, amountOption };
        cmd.SetHandler(async (string projectId, long amount) =>
        {
            var result = await investorService.IsInvestmentAbovePenaltyThreshold(
                new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(new ProjectId(projectId), new Amount(amount)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(result.Value.IsAboveThreshold
                ? "Amount is ABOVE the penalty threshold."
                : "Amount is below the penalty threshold.");
        }, projectIdOption, amountOption);
        return cmd;
    }

    private static Command BuildRecoveryStatusCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };

        var cmd = new Command("recovery-status", "Get recovery status for an investment") { walletIdOption, projectIdOption };
        cmd.SetHandler(async (string walletId, string projectId) =>
        {
            var result = await investorService.GetRecoveryStatus(
                new GetRecoveryStatus.GetRecoveryStatusRequest(new WalletId(walletId), new ProjectId(projectId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption);
        return cmd;
    }

    private static Command BuildBuildRecoveryCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };

        var cmd = new Command("build-recovery", "Build a recovery transaction") { walletIdOption, projectIdOption, feeRateOption };
        cmd.SetHandler(async (string walletId, string projectId, long feeRate) =>
        {
            var result = await investorService.BuildRecoveryTransaction(
                new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(
                    new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRate)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption, feeRateOption);
        return cmd;
    }

    private static Command BuildBuildReleaseCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };

        var cmd = new Command("build-release", "Build an unfunded release transaction") { walletIdOption, projectIdOption, feeRateOption };
        cmd.SetHandler(async (string walletId, string projectId, long feeRate) =>
        {
            var result = await investorService.BuildUnfundedReleaseTransaction(
                new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(
                    new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRate)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption, feeRateOption);
        return cmd;
    }

    private static Command BuildBuildPenaltyReleaseCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };

        var cmd = new Command("build-penalty-release", "Build a penalty release transaction") { walletIdOption, projectIdOption, feeRateOption };
        cmd.SetHandler(async (string walletId, string projectId, long feeRate) =>
        {
            var result = await investorService.BuildPenaltyReleaseTransaction(
                new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(
                    new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRate)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption, feeRateOption);
        return cmd;
    }

    private static Command BuildBuildEopClaimCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };

        var cmd = new Command("build-eop-claim", "Build an end-of-project claim transaction") { walletIdOption, projectIdOption, feeRateOption };
        cmd.SetHandler(async (string walletId, string projectId, long feeRate) =>
        {
            var result = await investorService.BuildEndOfProjectClaim(
                new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(
                    new WalletId(walletId), new ProjectId(projectId), new DomainFeerate(feeRate)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, projectIdOption, feeRateOption);
        return cmd;
    }

    private static Command BuildCheckSignaturesCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var projectIdOption = new Option<string>("--project-id", "Project ID") { IsRequired = true };

        var cmd = new Command("check-signatures", "Check if release signatures are available for an investment") { walletIdOption, projectIdOption };
        cmd.SetHandler(async (string walletId, string projectId) =>
        {
            var result = await investorService.CheckForReleaseSignatures(
                new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(new WalletId(walletId), new ProjectId(projectId)));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(result.Value.HasReleaseSignatures
                ? "Release signatures are available."
                : "No release signatures found.");
        }, walletIdOption, projectIdOption);
        return cmd;
    }

    private static Command BuildSubmitTxCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var txHexOption = new Option<string>("--tx-hex", "Signed transaction hex") { IsRequired = true };
        var txIdOption = new Option<string>("--tx-id", "Transaction ID") { IsRequired = true };
        var feeOption = new Option<long>("--fee", "Transaction fee in sats") { IsRequired = true };
        var walletIdOption = new Option<string?>("--wallet-id", "Wallet ID (optional)");
        var projectIdOption = new Option<string?>("--project-id", "Project ID (optional)");

        var cmd = new Command("submit-tx", "Submit a signed investor transaction") { txHexOption, txIdOption, feeOption, walletIdOption, projectIdOption };
        cmd.SetHandler(async (string txHex, string txId, long fee, string? walletId, string? projectId) =>
        {
            var draft = new TransactionDraft
            {
                SignedTxHex = txHex,
                TransactionId = txId,
                TransactionFee = new Amount(fee)
            };

            var result = await investorService.SubmitTransactionFromDraft(
                new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId, projectId != null ? new ProjectId(projectId) : null, draft));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Transaction published: {result.Value.TransactionId}");
        }, txHexOption, txIdOption, feeOption, walletIdOption, projectIdOption);
        return cmd;
    }

    private static Command BuildGetNsecCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var founderKeyOption = new Option<string>("--founder-key", "Founder public key") { IsRequired = true };

        var cmd = new Command("get-nsec", "Get investor Nostr secret key for a project") { walletIdOption, founderKeyOption };
        cmd.SetHandler(async (string walletId, string founderKey) =>
        {
            var result = await investorService.GetInvestorNsec(
                new GetInvestorNsec.GetInvestorNsecRequest(new WalletId(walletId), founderKey));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(result.Value.Nsec);
        }, walletIdOption, founderKeyOption);
        return cmd;
    }
}

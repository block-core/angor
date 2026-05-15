using System.CommandLine;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Cli.Commands.Wallet;

public static class WalletCommands
{
    public static Command Build(IServiceProvider services, JsonSerializerOptions jsonOptions)
    {
        var walletService = services.GetRequiredService<IWalletAppService>();

        var walletCommand = new Command("wallet", "Wallet management commands");

        walletCommand.AddCommand(BuildListCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildCreateCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildBalanceCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildBalanceDetailCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildReceiveCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildSendCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildTransactionsCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildFeeEstimatesCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildEstimateFeeCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildTestCoinsCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildDeleteCommand(walletService, jsonOptions));
        walletCommand.AddCommand(BuildGenerateSeedCommand(walletService));

        return walletCommand;
    }

    private static Command BuildListCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var jsonOption = new Option<bool>("--json", "Output as JSON");
        var cmd = new Command("list", "List all wallets") { jsonOption };
        cmd.SetHandler(async (bool json) =>
        {
            var result = await walletService.GetMetadatas();
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

            foreach (var wallet in result.Value)
            {
                Console.WriteLine($"  {wallet.Id}  {wallet.Name}");
            }
        }, jsonOption);
        return cmd;
    }

    private static Command BuildCreateCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var nameOption = new Option<string>("--name", "Wallet name") { IsRequired = true };
        var networkOption = new Option<string>("--network", () => "testnet", "Network (testnet or mainnet)");
        var seedOption = new Option<string?>("--seed", "BIP39 seed words (space-separated). Omit to generate.");

        var cmd = new Command("create", "Create a new wallet") { nameOption, networkOption, seedOption };
        cmd.SetHandler(async (string name, string networkStr, string? seed) =>
        {
            var network = networkStr.Equals("mainnet", StringComparison.OrdinalIgnoreCase)
                ? BitcoinNetwork.Mainnet
                : BitcoinNetwork.Testnet;

            Result<WalletId> result;
            if (!string.IsNullOrEmpty(seed))
            {
                result = await walletService.CreateWallet(name, seed, Maybe<string>.None, network);
            }
            else
            {
                result = await walletService.CreateWallet(name, network);
            }

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Wallet created: {result.Value}");
        }, nameOption, networkOption, seedOption);
        return cmd;
    }

    private static Command BuildBalanceCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("balance", "Get wallet balance") { walletIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, bool json) =>
        {
            var result = await walletService.GetBalance(new WalletId(walletId));
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

            var balance = result.Value;
            Console.WriteLine($"Balance: {balance}");
        }, walletIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildBalanceDetailCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("balance-detail", "Get detailed wallet account balance info") { walletIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, bool json) =>
        {
            var result = await walletService.GetAccountBalanceInfo(new WalletId(walletId));
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
        }, walletIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildReceiveCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };

        var cmd = new Command("receive", "Get next receive address") { walletIdOption };
        cmd.SetHandler(async (string walletId) =>
        {
            var result = await walletService.GetNextReceiveAddress(new WalletId(walletId));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(result.Value);
        }, walletIdOption);
        return cmd;
    }

    private static Command BuildSendCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var addressOption = new Option<string>("--address", "Destination address") { IsRequired = true };
        var amountOption = new Option<long>("--amount", "Amount in satoshis") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };

        var cmd = new Command("send", "Send bitcoin") { walletIdOption, addressOption, amountOption, feeRateOption };
        cmd.SetHandler(async (string walletId, string address, long amount, long feeRate) =>
        {
            var result = await walletService.SendAmount(
                new WalletId(walletId),
                new Amount(amount),
                new Address(address),
                new DomainFeeRate(feeRate));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine($"Transaction sent: {result.Value}");
        }, walletIdOption, addressOption, amountOption, feeRateOption);
        return cmd;
    }

    private static Command BuildTransactionsCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("transactions", "List wallet transactions") { walletIdOption, jsonOption };
        cmd.SetHandler(async (string walletId, bool json) =>
        {
            var result = await walletService.GetTransactions(new WalletId(walletId));
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

            foreach (var tx in result.Value)
            {
                Console.WriteLine($"  {tx.Id}  Fee={tx.Fee}");
            }
        }, walletIdOption, jsonOption);
        return cmd;
    }

    private static Command BuildFeeEstimatesCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("fee-estimates", "Get current fee estimates") { jsonOption };
        cmd.SetHandler(async (bool json) =>
        {
            var result = await walletService.GetFeeEstimates();
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

            foreach (var fee in result.Value)
            {
                Console.WriteLine($"  {fee.Confirmations} blocks: {fee.FeeRate} sat/vB");
            }
        }, jsonOption);
        return cmd;
    }

    private static Command BuildEstimateFeeCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var addressOption = new Option<string>("--address", "Destination address") { IsRequired = true };
        var amountOption = new Option<long>("--amount", "Amount in satoshis") { IsRequired = true };
        var feeRateOption = new Option<long>("--fee-rate", "Fee rate in sat/vB") { IsRequired = true };

        var cmd = new Command("estimate-fee", "Estimate transaction fee and size") { walletIdOption, addressOption, amountOption, feeRateOption };
        cmd.SetHandler(async (string walletId, string address, long amount, long feeRate) =>
        {
            var result = await walletService.EstimateFeeAndSize(
                new WalletId(walletId),
                new Amount(amount),
                new Address(address),
                new DomainFeeRate(feeRate));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, addressOption, amountOption, feeRateOption);
        return cmd;
    }

    private static Command BuildTestCoinsCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };

        var cmd = new Command("test-coins", "Request testnet coins") { walletIdOption };
        cmd.SetHandler(async (string walletId) =>
        {
            var result = await walletService.GetTestCoins(new WalletId(walletId));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine("Test coins requested successfully.");
        }, walletIdOption);
        return cmd;
    }

    private static Command BuildDeleteCommand(IWalletAppService walletService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };

        var cmd = new Command("delete", "Delete a wallet") { walletIdOption };
        cmd.SetHandler(async (string walletId) =>
        {
            var result = await walletService.DeleteWallet(new WalletId(walletId));
            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine("Wallet deleted.");
        }, walletIdOption);
        return cmd;
    }

    private static Command BuildGenerateSeedCommand(IWalletAppService walletService)
    {
        var cmd = new Command("generate-seed", "Generate random BIP39 seed words");
        cmd.SetHandler(() =>
        {
            var seedwords = walletService.GenerateRandomSeedwords();
            Console.WriteLine(seedwords);
        });
        return cmd;
    }
}

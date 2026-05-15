using System.ComponentModel;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;
using ModelContextProtocol.Server;

namespace Angor.Cli.McpTools;

[McpServerToolType]
public class WalletTools(IWalletAppService walletService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description("List all wallets with their IDs and names")]
    public async Task<string> WalletList()
    {
        var result = await walletService.GetMetadatas();
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Create a new wallet. Returns the wallet ID.")]
    public async Task<string> WalletCreate(string name, string network = "testnet", string? seedWords = null)
    {
        var bitcoinNetwork = network.Equals("mainnet", StringComparison.OrdinalIgnoreCase)
            ? BitcoinNetwork.Mainnet
            : BitcoinNetwork.Testnet;

        Result<WalletId> result;
        if (!string.IsNullOrEmpty(seedWords))
        {
            result = await walletService.CreateWallet(name, seedWords, Maybe<string>.None, bitcoinNetwork);
        }
        else
        {
            result = await walletService.CreateWallet(name, bitcoinNetwork);
        }

        return result.IsSuccess
            ? JsonSerializer.Serialize(new { walletId = result.Value.ToString() }, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get wallet balance in satoshis")]
    public async Task<string> WalletBalance(string walletId)
    {
        var result = await walletService.GetBalance(new WalletId(walletId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get detailed wallet account balance information")]
    public async Task<string> WalletBalanceDetail(string walletId)
    {
        var result = await walletService.GetAccountBalanceInfo(new WalletId(walletId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get the next unused receive address for a wallet")]
    public async Task<string> WalletReceive(string walletId)
    {
        var result = await walletService.GetNextReceiveAddress(new WalletId(walletId));
        return result.IsSuccess
            ? result.Value.ToString()
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Send bitcoin from a wallet to an address")]
    public async Task<string> WalletSend(string walletId, long amountSats, string address, long feeRateSatPerVb)
    {
        var result = await walletService.SendAmount(
            new WalletId(walletId),
            new Amount(amountSats),
            new Address(address),
            new DomainFeeRate(feeRateSatPerVb));

        return result.IsSuccess
            ? JsonSerializer.Serialize(new { transactionId = result.Value.ToString() }, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("List wallet transactions")]
    public async Task<string> WalletTransactions(string walletId)
    {
        var result = await walletService.GetTransactions(new WalletId(walletId));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Get current network fee estimates")]
    public async Task<string> WalletFeeEstimates()
    {
        var result = await walletService.GetFeeEstimates();
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Estimate transaction fee and size before sending")]
    public async Task<string> WalletEstimateFee(string walletId, long amountSats, string address, long feeRateSatPerVb)
    {
        var result = await walletService.EstimateFeeAndSize(
            new WalletId(walletId),
            new Amount(amountSats),
            new Address(address),
            new DomainFeeRate(feeRateSatPerVb));

        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Request testnet coins for a wallet")]
    public async Task<string> WalletTestCoins(string walletId)
    {
        var result = await walletService.GetTestCoins(new WalletId(walletId));
        return result.IsSuccess
            ? "Test coins requested successfully."
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Delete a wallet")]
    public async Task<string> WalletDelete(string walletId)
    {
        var result = await walletService.DeleteWallet(new WalletId(walletId));
        return result.IsSuccess
            ? "Wallet deleted."
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Generate random BIP39 seed words")]
    public string WalletGenerateSeed()
    {
        return walletService.GenerateRandomSeedwords();
    }
}

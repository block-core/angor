using System.CommandLine;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Cli.Commands.Lightning;

public static class LightningCommands
{
    public static Command Build(IServiceProvider services, JsonSerializerOptions jsonOptions)
    {
        var investorService = services.GetRequiredService<IInvestmentAppService>();

        var cmd = new Command("lightning", "Lightning Network swap commands");

        cmd.AddCommand(BuildCreateSwapCommand(investorService, jsonOptions));
        cmd.AddCommand(BuildMonitorSwapCommand(investorService, jsonOptions));

        return cmd;
    }

    private static Command BuildCreateSwapCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var claimPubKeyOption = new Option<string>("--claim-pubkey", "Claim public key") { IsRequired = true };
        var amountOption = new Option<long>("--amount", "Amount in sats") { IsRequired = true };
        var addressOption = new Option<string>("--receiving-address", "Receiving address") { IsRequired = true };
        var stageCountOption = new Option<int>("--stage-count", "Number of stages") { IsRequired = true };
        var feeRateOption = new Option<int>("--fee-rate", () => 2, "Estimated fee rate in sat/vB");

        var cmd = new Command("create-swap", "Create a Lightning submarine swap")
        {
            walletIdOption, claimPubKeyOption, amountOption, addressOption, stageCountOption, feeRateOption
        };
        cmd.SetHandler(async (string walletId, string claimPubKey, long amount, string address, int stageCount, int feeRate) =>
        {
            var result = await investorService.CreateLightningSwap(
                new CreateLightningSwap.CreateLightningSwapRequest(
                    new WalletId(walletId), claimPubKey, new Amount(amount), address, stageCount, feeRate));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, claimPubKeyOption, amountOption, addressOption, stageCountOption, feeRateOption);
        return cmd;
    }

    private static Command BuildMonitorSwapCommand(IInvestmentAppService investorService, JsonSerializerOptions jsonOptions)
    {
        var walletIdOption = new Option<string>("--wallet-id", "Wallet ID") { IsRequired = true };
        var swapIdOption = new Option<string>("--swap-id", "Swap ID to monitor") { IsRequired = true };
        var timeoutOption = new Option<int?>("--timeout", "Timeout in seconds");

        var cmd = new Command("monitor-swap", "Monitor a Lightning swap status") { walletIdOption, swapIdOption, timeoutOption };
        cmd.SetHandler(async (string walletId, string swapId, int? timeoutSecs) =>
        {
            TimeSpan? timeout = timeoutSecs.HasValue ? TimeSpan.FromSeconds(timeoutSecs.Value) : null;
            var result = await investorService.MonitorLightningSwap(
                new MonitorLightningSwap.MonitorLightningSwapRequest(new WalletId(walletId), swapId, timeout));

            if (result.IsFailure)
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(result.Value, jsonOptions));
        }, walletIdOption, swapIdOption, timeoutOption);
        return cmd;
    }
}

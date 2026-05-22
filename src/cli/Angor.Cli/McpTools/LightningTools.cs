using System.ComponentModel;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using ModelContextProtocol.Server;

namespace Angor.Cli.McpTools;

[McpServerToolType]
public class LightningTools(IInvestmentAppService investorService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description("Create a Lightning submarine swap for an investment")]
    public async Task<string> LightningCreateSwap(string walletId, string claimPublicKey, long amountSats,
        string receivingAddress, int stageCount, int estimatedFeeRateSatPerVb = 2)
    {
        var result = await investorService.CreateLightningSwap(
            new CreateLightningSwap.CreateLightningSwapRequest(
                new WalletId(walletId), claimPublicKey, new Amount(amountSats),
                receivingAddress, stageCount, estimatedFeeRateSatPerVb));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }

    [McpServerTool, Description("Monitor a Lightning swap status. Returns current status and claim transaction ID when complete.")]
    public async Task<string> LightningMonitorSwap(string walletId, string swapId, int? timeoutSeconds = null)
    {
        TimeSpan? timeout = timeoutSeconds.HasValue ? TimeSpan.FromSeconds(timeoutSeconds.Value) : null;
        var result = await investorService.MonitorLightningSwap(
            new MonitorLightningSwap.MonitorLightningSwapRequest(new WalletId(walletId), swapId, timeout));
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, JsonOptions)
            : $"Error: {result.Error}";
    }
}

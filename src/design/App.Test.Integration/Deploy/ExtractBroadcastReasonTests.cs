using Xunit;
using App.UI.Sections.MyProjects.Deploy;

namespace App.Test.Integration.Deploy;

public class ExtractBroadcastReasonTests
{
    [Fact]
    public void ExtractBroadcastReason_WhenJsonWrapped_ReturnsInnerMessageWithAdvice()
    {
        string raw = "Bad Request{\"error\":\"sendrawtransaction RPC error: {\\\"code\\\":-26,\\\"message\\\":\\\"bad-txns-inputs-missingorspent\\\"}\"}";

        string result = DeployFlowViewModel.ExtractBroadcastReason(raw);

        Assert.StartsWith("bad-txns-inputs-missingorspent", result);
        Assert.DoesNotContain("sendrawtransaction", result);
        Assert.Contains("Restart the app", result);
    }

    [Fact]
    public void ExtractBroadcastReason_WhenPlainText_ReturnsRawString()
    {
        Assert.Equal("Bad Requestsomething odd", DeployFlowViewModel.ExtractBroadcastReason("Bad Requestsomething odd"));
    }

    [Fact]
    public void ExtractBroadcastReason_WhenTooLong_IsTrimmed()
    {
        string result = DeployFlowViewModel.ExtractBroadcastReason(new string('x', 500));

        Assert.True(result.Length <= 203);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractBroadcastReason_WhenEmpty_ReturnsUnknown(string raw)
    {
        Assert.Equal("Unknown error", DeployFlowViewModel.ExtractBroadcastReason(raw));
    }

    [Fact]
    public void ExtractBroadcastReason_WhenGarbage_DoesNotThrowOrSwallow()
    {
        Assert.Contains("{{{", DeployFlowViewModel.ExtractBroadcastReason("{{{\"message\":"));
    }
}


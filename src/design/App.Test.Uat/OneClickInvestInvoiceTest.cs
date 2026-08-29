using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// UAT test covering all 4 combinations of payment method (on-chain / Lightning) × flow type (deploy / invest).
///
/// Founder (NO wallet created up front):
///   - Deploys an "investment" project, paying the deploy fee on-chain via faucet
///   - Deploys a "fund" project, paying the deploy fee via Lightning (ThunderHub)
///
/// Investor (NO wallet created up front — auto-created when investing via invoice):
///   - Invests in the Fund project via Lightning (ThunderHub)
///   - Invests in the Invest project via on-chain faucet
///
/// ThunderHub credentials are hardcoded constants (public test infrastructure).
/// </summary>
public class OneClickInvestInvoiceTest
{
    private const string TestName = "InvoiceInvest";
    private const string FounderProfile = TestName + "-Founder";
    private const string InvestorProfile = TestName + "-Investor";

    // ThunderHub testnet credentials (public test infrastructure)
    private const string ThunderHubUrl = "https://test.thub2.angor.io";
    private const string ThunderHubAccount = "lnd-2";
    private const string ThunderHubPassword = "123";

    [Fact]
    public async Task FullInvoiceFlow_DeployAndInvest_AllFourCombinations()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var investProjectName = $"InvProject {runId}";
        var fundProjectName = $"FndProject {runId}";
        var todayDay = DateTime.UtcNow.Day;

        Log($"========== STARTING {nameof(FullInvoiceFlow_DeployAndInvest_AllFourCombinations)} ==========");
        Log($"Run ID: {runId}");

        // ── Launch founder and investor app processes ──
        await using var founderHost = await TestHostFactory.LaunchAsync(FounderProfile);
        await founderHost.Client.WipeDataAsync();
        await founderHost.Client.SwitchNetworkAsync("Angornet");
        await founderHost.Client.EnableDebugModeAsync();

        await using var investorHost = await TestHostFactory.LaunchAsync(InvestorProfile);
        await investorHost.Client.WipeDataAsync();
        await investorHost.Client.SwitchNetworkAsync("Angornet");
        await investorHost.Client.EnableDebugModeAsync();

        // ════════════════════════════════════════════════════════════════
        // PHASE 1: Founder deploys "investment" project — pay deploy fee ON-CHAIN via faucet
        // ════════════════════════════════════════════════════════════════
        Log("Phase 1: Founder deploys Investment project (on-chain deploy fee)...");

        var deployInvestResult = await founderHost.Client.DeployViaInvoiceAsync(new DeployViaInvoiceRequest
        {
            ProjectType = "investment",
            Network = "onchain",
            ProjectName = investProjectName,
            ProjectAbout = $"{TestName} run {runId}inv. Investment project deployed via on-chain invoice.",
            BannerUrl = $"https://picsum.photos/seed/{runId}inv/320/200",
            ProfileUrl = $"https://picsum.photos/seed/{runId}inv/100/100",
            RunId = runId + "inv",
        });
        deployInvestResult.Success.Should().BeTrue(deployInvestResult.Error);
        deployInvestResult.Invoice.Should().NotBeNullOrEmpty("on-chain address should be returned for deploy fee");
        Log($"Deploy address (Investment): {deployInvestResult.Invoice}");

        // Pay deploy fee via faucet
        Log("Paying Investment deploy fee via faucet...");
        await PayViaFaucetAsync(deployInvestResult.Invoice!);

        // Wait for deploy to complete
        Log("Waiting for Investment project deploy...");
        var investProject = await founderHost.Client.WaitForDeployPaymentAsync(new WaitForDeployPaymentRequest
        {
            RunId = runId + "inv",
            TimeoutSeconds = 300,
        });
        investProject.Success.Should().BeTrue(investProject.Error);
        Log($"Investment project deployed: {investProject.ProjectIdentifier}");

        // ════════════════════════════════════════════════════════════════
        // PHASE 2: Founder deploys "fund" project — pay deploy fee via LIGHTNING
        // ════════════════════════════════════════════════════════════════
        Log("Phase 2: Founder deploys Fund project (Lightning deploy fee)...");

        var deployFundResult = await founderHost.Client.DeployViaInvoiceAsync(new DeployViaInvoiceRequest
        {
            ProjectType = "fund",
            Network = "lightning",
            ProjectName = fundProjectName,
            ProjectAbout = $"{TestName} run {runId}fnd. Fund project deployed via Lightning invoice.",
            BannerUrl = $"https://picsum.photos/seed/{runId}fnd/320/200",
            ProfileUrl = $"https://picsum.photos/seed/{runId}fnd/100/100",
            TargetAmountBtc = "1.0",
            ThresholdAmountBtc = "0.01",
            PenaltyDays = 0,
            PayoutFrequency = "Monthly",
            InstallmentCount = 3,
            MonthlyPayoutDay = todayDay,
            RunId = runId + "fnd",
        });
        deployFundResult.Success.Should().BeTrue(deployFundResult.Error);
        deployFundResult.Invoice.Should().NotBeNullOrEmpty("BOLT11 invoice should be returned for deploy fee");
        deployFundResult.Invoice.Should().StartWith("ln", "BOLT11 invoices start with 'ln' prefix");
        Log($"Lightning invoice (Fund deploy): {deployFundResult.Invoice![..60]}...");
        Log($"Boltz swap ID: {deployFundResult.SwapId}");

        // Pay deploy fee via ThunderHub
        Log("Paying Fund deploy fee via ThunderHub...");
        using (var thunderHub = new ThunderHubClient(ThunderHubUrl))
        {
            await thunderHub.LoginAsync(ThunderHubAccount, ThunderHubPassword);
            var paid = await thunderHub.PayInvoiceAsync(deployFundResult.Invoice);
            paid.Should().BeTrue("ThunderHub should successfully pay the deploy fee BOLT11 invoice");
        }

        // Wait for deploy to complete
        Log("Waiting for Fund project deploy...");
        var fundProject = await founderHost.Client.WaitForDeployPaymentAsync(new WaitForDeployPaymentRequest
        {
            RunId = runId + "fnd",
            TimeoutSeconds = 300,
        });
        fundProject.Success.Should().BeTrue(fundProject.Error);
        Log($"Fund project deployed: {fundProject.ProjectIdentifier}");

        // ════════════════════════════════════════════════════════════════
        // PHASE 3: Investor invests in Fund project via LIGHTNING
        // (wallet auto-created by PaymentFlowViewModel when generating invoice)
        // ════════════════════════════════════════════════════════════════
        Log("Phase 3: Investor invests in Fund project (Lightning)...");

        var investFundInvoice = await investorHost.Client.InvestViaInvoiceAsync(new InvestViaInvoiceRequest
        {
            ProjectIdentifier = fundProject.ProjectIdentifier!,
            RunId = runId + "fnd",
            ProjectName = fundProjectName,
            AmountBtc = "0.001",
            Network = "lightning",
        });
        investFundInvoice.Success.Should().BeTrue(investFundInvoice.Error);
        investFundInvoice.Invoice.Should().NotBeNullOrEmpty("BOLT11 invoice should be returned for investment");
        investFundInvoice.Invoice.Should().StartWith("ln", "BOLT11 invoices start with 'ln' prefix");
        Log($"Lightning invoice (Fund invest): {investFundInvoice.Invoice![..60]}...");

        // Pay via ThunderHub
        Log("Paying Fund investment via ThunderHub...");
        using (var thunderHub = new ThunderHubClient(ThunderHubUrl))
        {
            await thunderHub.LoginAsync(ThunderHubAccount, ThunderHubPassword);
            var paid = await thunderHub.PayInvoiceAsync(investFundInvoice.Invoice);
            paid.Should().BeTrue("ThunderHub should successfully pay the invest BOLT11 invoice");
        }

        // Wait for payment detection
        Log("Waiting for Fund investment payment detection...");
        var fundInvestResult = await investorHost.Client.WaitForInvoicePaymentAsync(
            new WaitForInvoicePaymentRequest { TimeoutSeconds = 300 });
        fundInvestResult.Success.Should().BeTrue(fundInvestResult.Error);
        Log($"Fund investment succeeded! AutoApproved: {fundInvestResult.IsAutoApproved}");

        // ════════════════════════════════════════════════════════════════
        // PHASE 4: Investor invests in Invest project via ON-CHAIN faucet
        // ════════════════════════════════════════════════════════════════
        Log("Phase 4: Investor invests in Investment project (on-chain)...");

        var investOnChain = await investorHost.Client.InvestViaInvoiceAsync(new InvestViaInvoiceRequest
        {
            ProjectIdentifier = investProject.ProjectIdentifier!,
            RunId = runId + "inv",
            ProjectName = investProjectName,
            AmountBtc = "0.001",
            Network = "onchain",
        });
        investOnChain.Success.Should().BeTrue(investOnChain.Error);
        investOnChain.Invoice.Should().NotBeNullOrEmpty("on-chain address should be returned for investment");
        Log($"On-chain address (Invest): {investOnChain.Invoice}");

        // Pay via faucet
        Log("Paying Investment via faucet...");
        await PayViaFaucetAsync(investOnChain.Invoice!);

        // Wait for payment detection
        Log("Waiting for Investment payment detection...");
        var investOnChainResult = await investorHost.Client.WaitForInvoicePaymentAsync(
            new WaitForInvoicePaymentRequest { TimeoutSeconds = 300 });
        investOnChainResult.Success.Should().BeTrue(investOnChainResult.Error);
        Log($"Investment succeeded! AutoApproved: {investOnChainResult.IsAutoApproved}");

        Log($"========== {nameof(FullInvoiceFlow_DeployAndInvest_AllFourCombinations)} PASSED ==========");
        Log("All 4 combinations verified:");
        Log("  Deploy Investment (on-chain) ✓");
        Log("  Deploy Fund (Lightning)      ✓");
        Log("  Invest Fund (Lightning)      ✓");
        Log("  Invest Investment (on-chain) ✓");
    }

    private static async Task PayViaFaucetAsync(string address)
    {
        var faucetBaseUrl = Environment.GetEnvironmentVariable("ANGOR_FAUCET_BASE_URL")
                           ?? "https://test.faucet.angor.io";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var faucetUrl = $"{faucetBaseUrl.TrimEnd('/')}/api/faucet/send/{address}/1";
        var faucetResponse = await http.GetAsync(faucetUrl);
        faucetResponse.IsSuccessStatusCode.Should().BeTrue(
            $"faucet should send funds. Status: {faucetResponse.StatusCode}, URL: {faucetUrl}");
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

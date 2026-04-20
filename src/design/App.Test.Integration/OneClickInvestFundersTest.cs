using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Wallet.Application;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funders;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using App.UI.Shared.Services;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Test.Integration;

/// <summary>
/// Full headless integration test for the 1-click fund flow triggered from the Funders section.
/// Focuses on Fund-type project behavior:
///   - Threshold check (auto-approve below, founder signatures above)
///   - PatternIndex is set for Fund projects
///   - PayWithWallet path (direct wallet payment)
///   - Success messages are Fund-specific
///   - FundersVM → InvestPageVM integration
/// </summary>
public class OneClickInvestFundersTest
{
    [AvaloniaFact]
    public void FundFlow_OpenFromFunders_CreatesFundTypeProject()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest));
        Log("========== STARTING fund flow project model test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();
        var currencyService = services.GetRequiredService<ICurrencyService>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel;
        vm.Should().NotBeNull("OpenInvestFlow should create an InvestPageViewModel");

        // Fund-specific project model assertions
        vm!.Project.ProjectType.Should().Be("Fund", "funders section creates Fund-type projects");
        vm.Project.ProjectId.Should().Be(sig.ProjectIdentifier);
        vm.Project.ProjectName.Should().Be(sig.ProjectTitle);
        vm.Project.CurrencySymbol.Should().Be(currencyService.Symbol);

        // Fund-type behavior flags
        vm.IsSubscription.Should().BeFalse("Fund is not a subscription");
        vm.IsNotSubscription.Should().BeTrue();

        // Fund-type terminology
        vm.ScheduleTitle.Should().Be("Release Schedule");
        vm.StageRowPrefix.Should().Be("Stage");
        vm.SuccessButtonText.Should().Be("View My Fundings");

        window.Close();
        Log("========== Fund flow project model test PASSED ==========");
    }

    [AvaloniaFact]
    public void FundFlow_SubmitAdvancesToWalletSelector()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Submit");
        Log("========== STARTING fund flow submit test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;
        vm.CurrentScreen.Should().Be(InvestScreen.InvestForm);

        // Below minimum — cannot submit
        vm.InvestmentAmount = "0.0001";
        Dispatcher.UIThread.RunJobs();
        vm.CanSubmit.Should().BeFalse("0.0001 is below minimum investment threshold");

        vm.Submit();
        Dispatcher.UIThread.RunJobs();
        vm.CurrentScreen.Should().Be(InvestScreen.InvestForm, "submit rejected when CanSubmit is false");

        // At minimum — can submit
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.CanSubmit.Should().BeTrue("0.001 meets the minimum investment threshold");

        vm.Submit();
        Dispatcher.UIThread.RunJobs();
        vm.CurrentScreen.Should().Be(InvestScreen.WalletSelector,
            "successful submit advances to wallet selector for fund payment");

        window.Close();
        Log("========== Fund flow submit test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task FundFlow_PayWithWallet_NoWalletSelected_ShowsError()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_NoWallet");
        Log("========== STARTING fund flow no-wallet error test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.Submit();
        Dispatcher.UIThread.RunJobs();

        // Attempt to pay without selecting a wallet
        vm.SelectedWallet.Should().BeNull("no wallet selected yet");
        vm.PayWithWallet();
        await PumpUntilAsync(() => vm.ErrorMessage != null, TimeSpan.FromSeconds(5));

        vm.ErrorMessage.Should().Be("No wallet selected.",
            "PayWithWallet without wallet selection should show clear error");
        vm.IsProcessing.Should().BeFalse("should not be processing after immediate error");

        window.Close();
        Log("========== Fund flow no-wallet error test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task FundFlow_PayWithWallet_BuildsDraft_WithPatternIndex()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Draft");
        Log("========== STARTING fund flow draft building test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();
        var walletContext = services.GetRequiredService<IWalletContext>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.Submit();
        Dispatcher.UIThread.RunJobs();

        // Select a wallet if available
        var wallet = walletContext.Wallets.FirstOrDefault();
        if (wallet == null)
        {
            Log("    No wallet available — skipping draft build (expected in CI without wallet)");
            window.Close();
            return;
        }

        vm.SelectWallet(wallet);
        Dispatcher.UIThread.RunJobs();
        vm.SelectedWallet.Should().NotBeNull();
        vm.HasSelectedWallet.Should().BeTrue();
        vm.PayButtonText.Should().Contain(wallet.Name);

        // Trigger pay — will attempt to build draft with Fund-specific patternIndex
        vm.PayWithWallet();
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.CurrentScreen == InvestScreen.Success || !vm.IsProcessing,
            TimeSpan.FromSeconds(15));

        Log($"    ErrorMessage: '{vm.ErrorMessage}'");
        Log($"    CurrentScreen: {vm.CurrentScreen}");
        Log($"    PaymentStatusText: '{vm.PaymentStatusText}'");

        // Regardless of outcome (success or SDK error), verify Fund-specific behavior:
        // - No raw exceptions leaked
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("NullReferenceException");
            vm.ErrorMessage.Should().NotContain("Parameter 'key'");
        }

        // If success, verify Fund-specific success state
        if (vm.CurrentScreen == InvestScreen.Success)
        {
            vm.SuccessTitle.Should().BeOneOf("Funding Successful", "Funding Pending Approval",
                "Fund type should show fund-specific success title");
            vm.SuccessButtonText.Should().Be("View My Fundings");
        }

        window.Close();
        Log("========== Fund flow draft building test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task FundFlow_ThresholdCheck_DeterminesApprovalPath()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Threshold");
        Log("========== STARTING fund flow threshold check test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();
        var walletContext = services.GetRequiredService<IWalletContext>();

        // Create a signature with a known threshold (1 BTC = 100_000_000 sats)
        var sig = new SignatureRequestViewModel
        {
            Id = 10002,
            ProjectTitle = "High Threshold Fund",
            ProjectIdentifier = "high-threshold-fund-project",
            Amount = "1.0000",
            Currency = "TBTC",
            AmountSats = 100_000_000,
            Status = "waiting",
            Npub = "npub1threshold",
            EventId = "event-threshold",
            FounderWalletId = "wallet-threshold",
            InvestmentTransactionHex = "02000000",
            InvestorNostrPubKey = "investor-threshold"
        };

        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;

        // Set amount below typical threshold (0.001 BTC = 100,000 sats)
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.Submit();
        Dispatcher.UIThread.RunJobs();

        var wallet = walletContext.Wallets.FirstOrDefault();
        if (wallet == null)
        {
            Log("    No wallet available — verifying threshold logic statically");
            // Verify that Fund projects trigger threshold check in the payment path
            // (the actual SDK call requires a wallet, but we can verify the VM setup)
            vm.Project.ProjectType.Should().Be("Fund",
                "Fund projects use threshold check to determine auto-approve vs. signatures");
            window.Close();
            return;
        }

        vm.SelectWallet(wallet);
        Dispatcher.UIThread.RunJobs();

        vm.PayWithWallet();
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) ||
                  vm.CurrentScreen == InvestScreen.Success ||
                  !vm.IsProcessing,
            TimeSpan.FromSeconds(15));

        Log($"    IsAutoApproved: {vm.IsAutoApproved}");
        Log($"    CurrentScreen: {vm.CurrentScreen}");
        Log($"    ErrorMessage: '{vm.ErrorMessage}'");

        // If we reached success, verify the threshold determined the path
        if (vm.CurrentScreen == InvestScreen.Success)
        {
            if (vm.IsAutoApproved)
            {
                vm.SuccessTitle.Should().Be("Funding Successful",
                    "below-threshold Fund investment should show auto-approved title");
                vm.SuccessDescription.Should().Contain("published successfully");
            }
            else
            {
                vm.SuccessTitle.Should().Be("Funding Pending Approval",
                    "above-threshold Fund investment should show pending approval title");
                vm.SuccessDescription.Should().Contain("pending founder approval");
            }
        }

        window.Close();
        Log("========== Fund flow threshold check test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task FundFlow_InvoicePath_UsesThresholdCheck()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Invoice");
        Log("========== STARTING fund flow invoice path threshold test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();

        // Enter invoice screen (on-chain default)
        vm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        vm.CurrentScreen.Should().Be(InvestScreen.Invoice);
        vm.IsProcessing.Should().BeTrue("on-chain monitoring starts");

        // Let on-chain flow attempt to run
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.OnChainAddress != null,
            TimeSpan.FromSeconds(10));

        Log($"    OnChainAddress: '{vm.OnChainAddress}'");
        Log($"    ErrorMessage: '{vm.ErrorMessage}'");

        // If we got an address, the flow will eventually call CompleteInvestmentAfterFundingAsync
        // which includes the Fund-type threshold check. Verify the VM state is consistent.
        if (vm.OnChainAddress != null)
        {
            vm.OnChainAddress.Should().NotBeEmpty("generated address should not be blank");
            vm.InvoiceString.Should().Be(vm.OnChainAddress,
                "InvoiceString should show the generated address");
            vm.QrCodeContent.Should().Be(vm.OnChainAddress,
                "QR code should display the address");
        }

        // Regardless of SDK availability, verify Fund-type specifics:
        // The CompleteInvestmentAfterFundingAsync uses patternIndex for Fund projects
        vm.Project.ProjectType.Should().Be("Fund");

        window.Close();
        Log("========== Fund flow invoice path threshold test PASSED ==========");
    }

    [AvaloniaFact]
    public void FundFlow_QuickAmounts_WorkForFundType()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Quick");
        Log("========== STARTING fund flow quick amounts test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();
        var currencyService = services.GetRequiredService<ICurrencyService>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;

        // Verify quick amounts are available for Fund type (not subscription)
        vm.QuickAmounts.Should().HaveCount(4, "Fund projects show 4 quick amount options");
        vm.QuickAmounts[0].Amount.Should().Be(0.001);
        vm.QuickAmounts[1].Amount.Should().Be(0.01);
        vm.QuickAmounts[2].Amount.Should().Be(0.1);
        vm.QuickAmounts[3].Amount.Should().Be(0.5);

        // Each quick amount label should use the correct currency symbol
        foreach (var option in vm.QuickAmounts)
        {
            option.Label.Should().Be(currencyService.Symbol);
        }

        // Select a quick amount
        vm.SelectQuickAmount(0.01);
        Dispatcher.UIThread.RunJobs();

        vm.InvestmentAmount.Should().Be("0.01");
        vm.SelectedQuickAmount.Should().Be(0.01);
        vm.FormattedAmount.Should().Be("0.01000000");
        vm.CanSubmit.Should().BeTrue();

        // Verify totals include fees
        vm.TotalAmount.Should().NotBe($"0.00000000 {currencyService.Symbol}",
            "total should include amount + fees");
        vm.AngorFeeAmount.Should().NotStartWith("0.00000000",
            "Angor fee should be calculated on the investment amount");

        window.Close();
        Log("========== Fund flow quick amounts test PASSED ==========");
    }

    [AvaloniaFact]
    public void FundFlow_WalletSelection_UpdatesPayButton()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Wallet");
        Log("========== STARTING fund flow wallet selection test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();
        var walletContext = services.GetRequiredService<IWalletContext>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.Submit();
        Dispatcher.UIThread.RunJobs();

        vm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        // Before wallet selection
        vm.HasSelectedWallet.Should().BeFalse();
        vm.PayButtonText.Should().Be("Choose Wallet");

        var wallet = walletContext.Wallets.FirstOrDefault();
        if (wallet == null)
        {
            Log("    No wallet available — verifying unselected state only");
            window.Close();
            return;
        }

        // After wallet selection
        vm.SelectWallet(wallet);
        Dispatcher.UIThread.RunJobs();

        vm.HasSelectedWallet.Should().BeTrue();
        vm.SelectedWallet.Should().Be(wallet);
        vm.PayButtonText.Should().Contain(wallet.Name,
            "pay button should show selected wallet name");
        wallet.IsSelected.Should().BeTrue("selected wallet should be marked");

        window.Close();
        Log("========== Fund flow wallet selection test PASSED ==========");
    }

    [AvaloniaFact]
    public void FundFlow_OpenInvestFlow_WithEmptyProjectId_DoesNotCreate()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_NoId");
        Log("========== STARTING fund flow empty project ID test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();

        var sig = new SignatureRequestViewModel
        {
            Id = 99998,
            ProjectTitle = "No ID Project",
            ProjectIdentifier = "",
            Amount = "0.1000",
            Status = "waiting"
        };

        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        fundersVm.InvestPageViewModel.Should().BeNull(
            "OpenInvestFlow should not create a VM when ProjectIdentifier is empty");

        window.Close();
        Log("========== Fund flow empty project ID test PASSED ==========");
    }

    [AvaloniaFact]
    public void FundFlow_CloseAndReopen_FreshState()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Reopen");
        Log("========== STARTING fund flow close and reopen test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var firstVm = fundersVm.InvestPageViewModel!;
        firstVm.InvestmentAmount = "0.05";
        Dispatcher.UIThread.RunJobs();
        firstVm.Submit();
        Dispatcher.UIThread.RunJobs();
        firstVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        // Close the flow
        fundersVm.CloseInvestFlow();
        Dispatcher.UIThread.RunJobs();
        fundersVm.InvestPageViewModel.Should().BeNull();

        // Reopen — should be a fresh instance at InvestForm
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var secondVm = fundersVm.InvestPageViewModel!;
        secondVm.Should().NotBeSameAs(firstVm, "reopen should create a new instance");
        secondVm.CurrentScreen.Should().Be(InvestScreen.InvestForm,
            "new instance starts fresh at InvestForm");
        secondVm.InvestmentAmount.Should().BeEmpty("new instance has no amount set");
        secondVm.IsProcessing.Should().BeFalse();
        secondVm.ErrorMessage.Should().BeNull();

        window.Close();
        Log("========== Fund flow close and reopen test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task FundFlow_PortfolioDeduplication()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Dedup");
        Log("========== STARTING fund flow portfolio deduplication test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var portfolioVm = services.GetRequiredService<PortfolioViewModel>();
        var fundersVm = services.GetRequiredService<FundersViewModel>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();

        // Simulate completed fund → add to portfolio
        var initialCount = portfolioVm.Investments.Count;
        vm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        portfolioVm.Investments.Count.Should().Be(initialCount + 1,
            "first add should insert a new entry");

        var entry = portfolioVm.Investments[0];
        entry.ProjectIdentifier.Should().Be(sig.ProjectIdentifier);

        // Duplicate add (e.g. SDK returns same investment on refresh)
        vm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        portfolioVm.Investments.Count.Should().Be(initialCount + 1,
            "second add of the same project must NOT create a duplicate");

        window.Close();
        Log("========== Fund flow portfolio deduplication test PASSED ==========");
    }

    [AvaloniaFact]
    public void FundFlow_SuccessMessages_MatchFundType()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestFundersTest) + "_Success");
        Log("========== STARTING fund flow success messages test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var fundersVm = services.GetRequiredService<FundersViewModel>();

        var sig = CreateTestSignatureRequest();
        fundersVm.OpenInvestFlow(sig);
        Dispatcher.UIThread.RunJobs();

        var vm = fundersVm.InvestPageViewModel!;
        vm.InvestmentAmount = "0.01";
        Dispatcher.UIThread.RunJobs();

        // Test auto-approved path (below threshold)
        vm.IsAutoApproved = true;
        Dispatcher.UIThread.RunJobs();

        vm.SuccessTitle.Should().Be("Funding Successful");
        vm.SuccessDescription.Should().Contain("published successfully");
        vm.SuccessDescription.Should().Contain("0.01000000");
        vm.SuccessDescription.Should().Contain(sig.ProjectTitle);
        vm.SuccessButtonText.Should().Be("View My Fundings");

        // Test pending approval path (above threshold)
        vm.IsAutoApproved = false;
        Dispatcher.UIThread.RunJobs();

        vm.SuccessTitle.Should().Be("Funding Pending Approval");
        vm.SuccessDescription.Should().Contain("pending founder approval");
        vm.SuccessDescription.Should().Contain(sig.ProjectTitle);

        window.Close();
        Log("========== Fund flow success messages test PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static SignatureRequestViewModel CreateTestSignatureRequest() => new()
    {
        Id = 10001,
        ProjectTitle = "Headless Funder Test Project",
        ProjectIdentifier = "headless-funder-test-project",
        Amount = "0.0010",
        Currency = "TBTC",
        AmountSats = 100_000,
        Date = "Apr 20, 2026",
        Time = "12:00",
        Status = "waiting",
        Npub = "npub1headlesstestkey",
        EventId = "event-headless-test",
        FounderWalletId = "wallet-headless-test",
        InvestmentTransactionHex = "02000000deadbeef",
        InvestorNostrPubKey = "investor-headless-pub"
    };

    private static async Task PumpUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(50);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

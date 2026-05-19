using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Wallet.Application;
using Angor.Shared.Utilities;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funders;
using App.UI.Sections.Funds;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Sections.MyProjects.EditProfile;
using App.UI.Sections.Portfolio;
using App.UI.Sections.Settings;
using App.UI.Shared.Controls;
using App.UI.Shared.PaymentFlow;
using App.UI.Shared.Services;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using static App.Automation.AutomationDtos;
using static App.Automation.AutomationFlowDtos;

namespace App.Automation;

public static class AutomationFlows
{
    private static readonly TimeSpan FaucetTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TxTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IndexerLag = TimeSpan.FromMinutes(10);

    public static async Task<CreateWalletAndFundResponse> CreateWalletAndFundAsync(
        IServiceProvider services,
        CreateWalletAndFundRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "Funds");

            var fundsVm = await Dispatcher.UIThread.InvokeAsync(() => GetFundsViewModel(window));
            if (fundsVm == null)
            {
                return new CreateWalletAndFundResponse { Success = false, Error = "FundsViewModel not found" };
            }

            var hasWallet = await Dispatcher.UIThread.InvokeAsync(() =>
                fundsVm.SeedGroups.Any() && fundsVm.SeedGroups.SelectMany(g => g.Wallets).Any());

            if (!hasWallet)
            {
                Log(req.ProfileName, "Creating wallet via Generate flow...");
                await CreateWalletViaGenerateAsync(window);
            }

            var walletId = await Dispatcher.UIThread.InvokeAsync(() =>
                fundsVm.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value);

            if (string.IsNullOrWhiteSpace(walletId))
            {
                return new CreateWalletAndFundResponse { Success = false, Error = "Wallet id not found after wallet creation" };
            }

            Log(req.ProfileName, "Funding wallet via faucet...");
            await FundWalletViaFaucetAsync(window, fundsVm, walletId, req.ProfileName);

            return new CreateWalletAndFundResponse
            {
                Success = true,
                WalletId = walletId,
            };
        }
        catch (Exception ex)
        {
            return new CreateWalletAndFundResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<ProjectCreatedResponse> CreateFundProjectAsync(
        IServiceProvider services,
        CreateFundProjectRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "My Projects");

            var myProjectsVm = await Dispatcher.UIThread.InvokeAsync(() => GetMyProjectsViewModel(window));
            if (myProjectsVm == null)
            {
                return new ProjectCreatedResponse { Success = false, Error = "MyProjectsViewModel not found" };
            }

            await OpenCreateWizardAsync(myProjectsVm, window);

            // Step 0: Welcome → click Start
            await ClickByNameAsync(window, "StartButton");

            // Step 1: Select "fund" type → click TypeFundCard, then Next
            await ClickByNameAsync(window, "TypeFundCard");
            await ClickByNameAsync(window, "NextStepButton");

            // Step 2: Project name + about → type text, then Next
            await TypeTextByNameAsync(window, "ProjectNameTextBox", req.ProjectName);
            await TypeTextByNameAsync(window, "AboutTextBox", req.ProjectAbout);
            await ClickByNameAsync(window, "NextStepButton");

            // Step 3: Banner + profile URLs → type text, then Next
            await TypeTextByNameAsync(window, "BannerUrlTextBox", req.BannerUrl);
            await TypeTextByNameAsync(window, "ProfileUrlTextBox", req.ProfileUrl);
            await ClickByNameAsync(window, "NextStepButton");

            // Step 4: Target amount + approval threshold + penalty days
            await TypeTextByNameAsync(window, "FundTargetAmountInput", "1.0");
            await TypeTextByNameAsync(window, "ApprovalThresholdInput", req.ThresholdAmountBtc);
            // PenaltyDays has no UI input for fund type — set via VM (defaults to 0)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var wizardVm = myProjectsVm.CreateProjectVm;
                wizardVm.PenaltyDays = 0;
                Dispatcher.UIThread.RunJobs();
            });
            await ClickByNameAsync(window, "NextStepButton");

            // Step 5 interstitial: Dismiss welcome
            await ClickByNameAsync(window, "Step5WelcomeButton");
            await Task.Delay(200);

            // Step 5: Payout frequency + installments + day + generate
            await ClickByNameAsync(window, "PayoutFreqWeekly");
            await ClickByNameAsync(window, "Installment3");
            await ClickByNameAsync(window, "Installment6");

            // Select payout day via VM (ListBox selection is not easily clickable by Name)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var wizardVm = myProjectsVm.CreateProjectVm;
                wizardVm.WeeklyPayoutDay = req.PayoutDay;
                Dispatcher.UIThread.RunJobs();
            });

            await ClickByNameAsync(window, "GeneratePayoutsButton");
            await ClickByNameAsync(window, "NextStepButton");

            return await DeployProjectAsync(myProjectsVm, window, req.RunId, "fund");
        }
        catch (Exception ex)
        {
            return new ProjectCreatedResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<ProjectCreatedResponse> CreateInvestProjectAsync(
        IServiceProvider services,
        CreateInvestProjectRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "My Projects");

            var myProjectsVm = await Dispatcher.UIThread.InvokeAsync(() => GetMyProjectsViewModel(window));
            if (myProjectsVm == null)
            {
                return new ProjectCreatedResponse { Success = false, Error = "MyProjectsViewModel not found" };
            }

            await OpenCreateWizardAsync(myProjectsVm, window);

            // Step 0: Welcome → click Start
            await ClickByNameAsync(window, "StartButton");

            // Step 1: Select "investment" type → click TypeInvestCard, then Next
            await ClickByNameAsync(window, "TypeInvestCard");
            await ClickByNameAsync(window, "NextStepButton");

            // Step 2: Project name + about → type text, then Next
            await TypeTextByNameAsync(window, "ProjectNameTextBox", req.ProjectName);
            await TypeTextByNameAsync(window, "AboutTextBox", req.ProjectAbout);
            await ClickByNameAsync(window, "NextStepButton");

            // Step 3: Banner + profile URLs → type text, then Next
            await TypeTextByNameAsync(window, "BannerUrlTextBox", req.BannerUrl);
            await TypeTextByNameAsync(window, "ProfileUrlTextBox", req.ProfileUrl);
            await ClickByNameAsync(window, "NextStepButton");

            // Step 4: Target amount + invest end date (set via CalendarDatePicker control)
            await TypeTextByNameAsync(window, "InvestTargetAmountInput", "1.0");
            await SetCalendarDateByNameAsync(window, "InvestEndDatePicker", DateTime.UtcNow.AddMonths(3));
            await ClickByNameAsync(window, "NextStepButton");

            // Step 5 interstitial: Dismiss welcome
            await ClickByNameAsync(window, "Step5WelcomeButton");
            await Task.Delay(200);

            // Step 5: Duration + frequency + start date + generate stages
            await TypeTextByNameAsync(window, "DurationValueInput", "3");
            // Set DurationUnit via ComboBox control
            await SetComboBoxByNameAsync(window, "DurationUnitCombo", "Months");
            // Click "Monthly" ListBoxItem in InvestFrequencyPresets
            await ClickListBoxItemByTagAsync(window, "InvestFrequencyPresets", "Monthly");
            // Set StartDate via CalendarDatePicker control
            await SetCalendarDateByNameAsync(window, "InvestStartDatePicker", DateTime.UtcNow.AddDays(-120));

            await ClickByNameAsync(window, "GenerateStagesButton");
            await ClickByNameAsync(window, "NextStepButton");

            return await DeployProjectAsync(myProjectsVm, window, req.RunId, "investment");
        }
        catch (Exception ex)
        {
            return new ProjectCreatedResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<InvestResponse> InvestInProjectAsync(
        IServiceProvider services,
        InvestInProjectRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "Find Projects");

            var findProjectsVm = await Dispatcher.UIThread.InvokeAsync(() => GetFindProjectsViewModel(window));
            if (findProjectsVm == null)
            {
                return new InvestResponse { Success = false, Error = "FindProjectsViewModel not found" };
            }

            ProjectItemViewModel? foundProject = null;
            var projectDeadline = DateTime.UtcNow + IndexerLag;
            while (DateTime.UtcNow < projectDeadline)
            {
                foundProject = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await findProjectsVm.LoadProjectsFromSdkAsync();
                    // Drain all pages so we search the full project list
                    while (findProjectsVm.HasMoreItems)
                    {
                        findProjectsVm.LoadMore();
                    }
                    Dispatcher.UIThread.RunJobs();
                    return findProjectsVm.Projects.FirstOrDefault(p =>
                        string.Equals(p.ProjectId, req.ProjectIdentifier, StringComparison.Ordinal) ||
                        p.Description.Contains(req.RunId, StringComparison.Ordinal) ||
                        p.ShortDescription.Contains(req.RunId, StringComparison.Ordinal));
                });

                if (foundProject != null)
                {
                    break;
                }

                await Task.Delay(PollInterval);
            }

            if (foundProject == null)
            {
                return new InvestResponse { Success = false, Error = "Project not found in SDK list" };
            }

            // Click the ProjectCard in the visual tree to open project detail
            // FindProjectsView uses TappedEvent bubbling — we call OpenProjectDetail via VM
            // since Tapped requires complex pointer event args. The card click IS the user action,
            // we just can't synthesize Tapped easily in Avalonia.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                findProjectsVm.OpenProjectDetail(foundProject);
                Dispatcher.UIThread.RunJobs();
            });
            await Task.Delay(500);

            // Click InvestButton in ProjectDetailView to navigate to invest page
            await ClickByNameAsync(window, "InvestButton");
            await Task.Delay(500);

            var investVm = await Dispatcher.UIThread.InvokeAsync(() => findProjectsVm.InvestPageViewModel);
            if (investVm == null)
            {
                return new InvestResponse { Success = false, Error = "InvestPageViewModel not found" };
            }

            var walletDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < walletDeadline)
            {
                var walletCount = await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Dispatcher.UIThread.RunJobs();
                    return investVm.Wallets.Count;
                });

                if (walletCount > 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            // Select funding pattern via UI click if requested
            if (req.TargetPatternStageCount > 0)
            {
                await ClickFundPatternByStageCountAsync(window, req.TargetPatternStageCount);
            }

            // Type investment amount into AmountInput TextBox, then click SubmitButton
            await TypeTextByNameAsync(window, "AmountInput", req.AmountBtc);
            await ClickByNameAsync(window, "SubmitButton");

            // Wait for payment flow modal to appear (SubmitButton triggers shell modal)
            await Task.Delay(500);

            var maxPayAttempts = 3;
            for (var payAttempt = 1; payAttempt <= maxPayAttempts; payAttempt++)
            {
                // Click the first WalletButton in the payment flow, then PayWithWalletButton
                await ClickFirstWalletButtonAsync(window);
                await ClickByNameAsync(window, "PayWithWalletButton");

                // FeeSelectionPopup appears — click ConfirmButton (defaults to "standard" 20 sat/vB)
                await ClickByNameAsync(window, "ConfirmButton", TimeSpan.FromSeconds(10));
                await Task.Delay(300);

                var investDeadline = DateTime.UtcNow + TxTimeout;
                while (DateTime.UtcNow < investDeadline)
                {
                    var completed = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return investVm.PaymentFlow.CurrentScreen == PaymentFlowScreen.Success
                            || (investVm.CurrentScreen == InvestScreen.WalletSelector && investVm.PaymentFlow.ErrorMessage != null);
                    });

                    if (completed)
                    {
                        break;
                    }

                    await Task.Delay(PollInterval);
                }

                var success = await Dispatcher.UIThread.InvokeAsync(() =>
                    investVm.PaymentFlow.CurrentScreen == PaymentFlowScreen.Success);
                if (success)
                {
                    break;
                }

                var shouldRetry = await Dispatcher.UIThread.InvokeAsync(() =>
                    payAttempt < maxPayAttempts
                    && investVm.CurrentScreen == InvestScreen.WalletSelector
                    && investVm.PaymentFlow.ErrorMessage != null);

                if (!shouldRetry)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            var paySucceeded = await Dispatcher.UIThread.InvokeAsync(() =>
                investVm.PaymentFlow.CurrentScreen == PaymentFlowScreen.Success);
            if (!paySucceeded)
            {
                var error = await Dispatcher.UIThread.InvokeAsync(() => investVm.PaymentFlow.ErrorMessage);
                return new InvestResponse { Success = false, Error = error ?? "Investment payment did not reach success" };
            }

            // Click SuccessActionButton (triggers AddToPortfolio + navigates to Funded)
            await ClickByNameAsync(window, "SuccessActionButton");

            var isAutoApproved = await Dispatcher.UIThread.InvokeAsync(() => investVm.IsAutoApproved);
            return new InvestResponse { Success = true, IsAutoApproved = isAutoApproved };
        }
        catch (Exception ex)
        {
            return new InvestResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<ApproveInvestmentsResponse> ApproveInvestmentsAsync(
        IServiceProvider services,
        ApproveInvestmentsRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "Funders");

            var fundersVm = await Dispatcher.UIThread.InvokeAsync(() => GetFundersViewModel(window));
            if (fundersVm == null)
            {
                return new ApproveInvestmentsResponse { Success = false, Error = "FundersViewModel not found" };
            }

            var expectedCount = req.Batch ? req.ExpectedCount : 1;
            var deadline = DateTime.UtcNow + IndexerLag;
            SignatureRequestViewModel[] pending;

            while (true)
            {
                pending = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await fundersVm.LoadInvestmentRequestsAsync();
                    Dispatcher.UIThread.RunJobs();
                    fundersVm.SetFilter("waiting");
                    Dispatcher.UIThread.RunJobs();
                    return fundersVm.FilteredSignatures
                        .Where(s => string.Equals(s.ProjectIdentifier, req.ProjectIdentifier, StringComparison.Ordinal))
                        .OrderBy(s => s.AmountSats)
                        .Take(expectedCount)
                        .ToArray();
                });

                if (pending.Length >= expectedCount)
                {
                    break;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return new ApproveInvestmentsResponse { Success = false, Error = "Pending approvals did not appear in time" };
                }

                await Task.Delay(PollInterval);
            }

            foreach (var pendingSignature in pending)
            {
                // Try clicking the ApproveButton in the visual tree first (virtualized ListBox)
                // If not found, scroll the item into view and retry
                var clickedViaUi = await ClickApproveButtonByIdAsync(window, pendingSignature.Id);

                if (!clickedViaUi)
                {
                    // Scroll the ListBox to bring the item into view, then retry click
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(lb =>
                            lb.ItemsSource is System.Collections.IEnumerable items &&
                            items.Cast<object>().Any(i => i is SignatureRequestViewModel));
                        if (listBox != null)
                        {
                            listBox.ScrollIntoView(pendingSignature);
                            Dispatcher.UIThread.RunJobs();
                        }
                    });
                    await Task.Delay(200);

                    clickedViaUi = await ClickApproveButtonByIdAsync(window, pendingSignature.Id);
                }

                if (!clickedViaUi)
                {
                    // Final fallback: call the VM's public ApproveSignature method
                    // (handles edge cases where scroll doesn't materialize the item)
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        fundersVm.ApproveSignature(pendingSignature.Id);
                        Dispatcher.UIThread.RunJobs();
                    });
                }
            }

            var approvalDeadline = DateTime.UtcNow + IndexerLag;
            while (DateTime.UtcNow < approvalDeadline)
            {
                var approvedCount = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await fundersVm.LoadInvestmentRequestsAsync();
                    Dispatcher.UIThread.RunJobs();
                    fundersVm.SetFilter("approved");
                    Dispatcher.UIThread.RunJobs();
                    return fundersVm.FilteredSignatures.Count(s =>
                        string.Equals(s.ProjectIdentifier, req.ProjectIdentifier, StringComparison.Ordinal));
                });

                if (approvedCount >= expectedCount)
                {
                    return new ApproveInvestmentsResponse { Success = true, ApprovedCount = approvedCount };
                }

                await Task.Delay(PollInterval);
            }

            return new ApproveInvestmentsResponse { Success = false, Error = "Approvals did not complete in time" };
        }
        catch (Exception ex)
        {
            return new ApproveInvestmentsResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<ConfirmInvestmentResponse> ConfirmInvestmentAsync(
        IServiceProvider services,
        ConfirmInvestmentRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "Funded");

            var portfolioVm = services.GetRequiredService<PortfolioViewModel>();

            // Wait for the investment to appear at step >= 2 (founder-signed)
            var investment = await WaitForInvestmentAsync(portfolioVm, req.ProjectIdentifier, i => i.Step >= 2);
            if (investment == null)
            {
                return new ConfirmInvestmentResponse { Success = false, Error = "Approved investment not found" };
            }

            // Click RefreshButton to ensure UI is up-to-date
            await ClickByNameAsync(window, "RefreshButton");
            await Task.Delay(500);

            // Click the ManageButton for this investment
            await ClickManageButtonByProjectAsync(window, req.ProjectIdentifier);
            await Task.Delay(500);

            // Click ConfirmInvestmentButton in the InvestmentDetailView
            await ClickByNameAsync(window, "ConfirmInvestmentButton", TimeSpan.FromSeconds(30));

            // Wait for investment to reach step 3
            var confirmed = await WaitForInvestmentAsync(portfolioVm, req.ProjectIdentifier, i => i.Step == 3);
            if (confirmed == null)
            {
                return new ConfirmInvestmentResponse { Success = false, Error = "Investment did not become active in time" };
            }

            return new ConfirmInvestmentResponse { Success = true, Step = confirmed.Step };
        }
        catch (Exception ex)
        {
            return new ConfirmInvestmentResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<ActionResponse> ClaimStageAsync(
        IServiceProvider services,
        ClaimStageRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "My Projects");

            var myProjectsVm = await Dispatcher.UIThread.InvokeAsync(() => GetMyProjectsViewModel(window));
            if (myProjectsVm == null)
            {
                return new ActionResponse { Success = false, Error = "MyProjectsViewModel not found" };
            }

            var founderProject = await WaitForFounderProjectAsync(myProjectsVm, req.ProjectIdentifier);
            if (founderProject == null)
            {
                return new ActionResponse { Success = false, Error = "Founder project not found" };
            }

            var deadline = DateTime.UtcNow + IndexerLag;
            while (DateTime.UtcNow < deadline)
            {
                var ready = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await myProjectsVm.LoadFounderProjectsAsync();
                    // Re-fetch the project reference after reload (collection is cleared and repopulated)
                    var currentProject = myProjectsVm.Projects.FirstOrDefault(p =>
                        string.Equals(p.ProjectIdentifier, founderProject.ProjectIdentifier, StringComparison.Ordinal));
                    if (currentProject == null) return false;

                    myProjectsVm.OpenManageProject(currentProject);
                    var manageVm = myProjectsVm.SelectedManageProject;
                    if (manageVm == null)
                    {
                        return false;
                    }

                    await manageVm.LoadClaimableTransactionsAsync();
                    Dispatcher.UIThread.RunJobs();

                    return manageVm.Stages.Any(s =>
                        s.Number == req.StageNumber && s.AvailableTransactions.Count >= req.ExpectedUtxoCount);
                });

                if (ready)
                {
                    break;
                }

                // Close manage panel so next iteration can re-open with fresh data
                await Dispatcher.UIThread.InvokeAsync(() => myProjectsVm.CloseManageProject());

                await Task.Delay(PollInterval);
            }

            // Close manage panel so we can click through the UI
            await Dispatcher.UIThread.InvokeAsync(() => myProjectsVm.CloseManageProject());
            await Task.Delay(200);

            var clicked = await ClickManageProjectClaimStageAsync(window, myProjectsVm, founderProject, req.StageNumber);

            // Close manage panel so subsequent flows (e.g. ReleaseFunds) can find project cards
            await Dispatcher.UIThread.InvokeAsync(() => myProjectsVm.CloseManageProject());
            await Task.Delay(200);

            return clicked
                ? new ActionResponse { Success = true }
                : new ActionResponse { Success = false, Error = "Claim stage flow did not complete" };
        }
        catch (Exception ex)
        {
            return new ActionResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<RecoveryResponse> ExecuteRecoveryAsync(
        IServiceProvider services,
        RecoveryRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "Funded");

            var portfolioVm = services.GetRequiredService<PortfolioViewModel>();
            var investment = await WaitForInvestmentAsync(portfolioVm, req.ProjectIdentifier, i => i.Step == 3);
            if (investment == null)
            {
                return new RecoveryResponse { Success = false, Error = "Active investment not found" };
            }

            if (!string.IsNullOrWhiteSpace(investment.InvestmentWalletId))
            {
                var feeResult = await EnsureWalletFeeFundsAsync(services, investment.InvestmentWalletId, req.ProjectIdentifier);
                if (!feeResult.Success)
                {
                    return new RecoveryResponse { Success = false, Error = feeResult.Error };
                }

                // Re-navigate to Funded after fee funding
                await NavigateToAsync(window, "Funded");
            }

            // Click RefreshButton to ensure portfolio is up-to-date
            await ClickByNameAsync(window, "RefreshButton");
            await Task.Delay(1000);

            // Click ManageButton for this investment to open detail
            await ClickManageButtonByProjectAsync(window, req.ProjectIdentifier);
            await Task.Delay(500);

            // Re-fetch the current investment VM (RefreshButton/ManageButton may have recreated VMs)
            var currentInvestment = await Dispatcher.UIThread.InvokeAsync(() =>
                portfolioVm.SelectedInvestment ?? portfolioVm.Investments.FirstOrDefault(i =>
                    string.Equals(i.ProjectIdentifier, req.ProjectIdentifier, StringComparison.Ordinal)));

            if (currentInvestment == null)
            {
                return new RecoveryResponse { Success = false, Error = "Investment VM not found after navigation" };
            }

            // Wait for recovery status to load and RecoverFundsButton to appear
            var recoveryDeadline = DateTime.UtcNow + IndexerLag;
            while (DateTime.UtcNow < recoveryDeadline)
            {
                var actionKey = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // Re-fetch current investment VM (may be recreated by refresh)
                    currentInvestment = portfolioVm.SelectedInvestment
                        ?? portfolioVm.Investments.FirstOrDefault(i =>
                            string.Equals(i.ProjectIdentifier, req.ProjectIdentifier, StringComparison.Ordinal));
                    if (currentInvestment == null) return "none";

                    await portfolioVm.LoadRecoveryStatusAsync(currentInvestment);
                    Dispatcher.UIThread.RunJobs();
                    return currentInvestment.RecoveryState.ActionKey;
                });

                if (string.Equals(actionKey, req.Action, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // Click RefreshDetailButton to update
                try { await ClickByNameAsync(window, "RefreshDetailButton", TimeSpan.FromSeconds(3)); }
                catch (TimeoutException) { /* button may not exist */ }

                await Task.Delay(PollInterval);
            }

            // Click RecoverFundsButton to launch recovery modals
            await ClickByNameAsync(window, "RecoverFundsButton", TimeSpan.FromSeconds(30));
            await Task.Delay(500);

            // Click the appropriate confirm button based on recovery action
            var confirmButtonName = req.Action switch
            {
                "recovery" => "ConfirmRecoveryModal",
                "belowThreshold" => "ConfirmRecoveryModal",
                "endOfProject" => "ClaimPenaltyButton",
                "unfundedRelease" => "ConfirmReleaseModal",
                "penaltyRelease" => "ConfirmReleaseModal",
                _ => throw new InvalidOperationException($"Unknown recovery action: {req.Action}")
            };

            await ClickByNameAsync(window, confirmButtonName);
            await Task.Delay(300);

            // FeeSelectionPopup appears — click ConfirmButton (defaults to "standard" 20 sat/vB)
            await ClickByNameAsync(window, "ConfirmButton", TimeSpan.FromSeconds(10));

            // Wait for success modal to appear
            var successDeadline = DateTime.UtcNow + IndexerLag;
            while (DateTime.UtcNow < successDeadline)
            {
                var showSuccess = await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Dispatcher.UIThread.RunJobs();
                    // Re-fetch current investment VM (may have been replaced)
                    currentInvestment = portfolioVm.SelectedInvestment
                        ?? portfolioVm.Investments.FirstOrDefault(i =>
                            string.Equals(i.ProjectIdentifier, req.ProjectIdentifier, StringComparison.Ordinal));
                    return currentInvestment?.ShowSuccessModal == true;
                });

                if (showSuccess)
                {
                    return new RecoveryResponse { Success = true, ActionKey = req.Action };
                }

                // Check for error
                var errorMsg = await Dispatcher.UIThread.InvokeAsync(() => currentInvestment?.ErrorMessage);
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    return new RecoveryResponse { Success = false, Error = errorMsg };
                }

                await Task.Delay(PollInterval);
            }

            return new RecoveryResponse { Success = false, Error = $"Recovery action '{req.Action}' did not complete in time" };
        }
        catch (Exception ex)
        {
            return new RecoveryResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<ActionResponse> ReleaseFundsToInvestorsAsync(
        IServiceProvider services,
        ReleaseFundsRequest req)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "My Projects");

            var myProjectsVm = await Dispatcher.UIThread.InvokeAsync(() => GetMyProjectsViewModel(window));
            if (myProjectsVm == null)
            {
                return new ActionResponse { Success = false, Error = "MyProjectsViewModel not found" };
            }

            var founderProject = await WaitForFounderProjectAsync(myProjectsVm, req.ProjectIdentifier);
            if (founderProject == null)
            {
                return new ActionResponse { Success = false, Error = "Founder project not found" };
            }

            var success = await ClickManageProjectReleaseFundsAsync(window, myProjectsVm, founderProject);

            // Close manage panel so subsequent flows can find project cards
            await Dispatcher.UIThread.InvokeAsync(() => myProjectsVm.CloseManageProject());
            await Task.Delay(200);

            return success
                ? new ActionResponse { Success = true }
                : new ActionResponse { Success = false, Error = "Release funds flow did not complete" };
        }
        catch (Exception ex)
        {
            return new ActionResponse { Success = false, Error = ex.Message };
        }
    }

    public static async Task<ActionResponse> EnsureWalletFeeFundsAsync(
        IServiceProvider services,
        string walletId,
        string profileName)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "Funds");

            var fundsVm = await Dispatcher.UIThread.InvokeAsync(() => GetFundsViewModel(window));
            if (fundsVm == null)
            {
                return new ActionResponse { Success = false, Error = "FundsViewModel not found" };
            }

            var walletAppService = services.GetRequiredService<IWalletAppService>();
            var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
            while (DateTime.UtcNow < deadline)
            {
                var refresh = await walletAppService.RefreshAndGetAccountBalanceInfo(new WalletId(walletId));
                if (refresh.IsSuccess)
                {
                    var info = refresh.Value;
                    var available = info.TotalBalance + info.TotalUnconfirmedBalance + info.TotalBalanceReserved;
                    Log(profileName, $"Wallet fee balance: {available.ToUnitBtc():F8} BTC");
                    if (available > 20_000)
                    {
                        return new ActionResponse { Success = true };
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await fundsVm.GetTestCoinsAsync(walletId);
                    Dispatcher.UIThread.RunJobs();
                });

                await ClickWalletCardButtonAsync(window, "WalletCardBtnRefresh");
                await Task.Delay(PollInterval);
            }

            return new ActionResponse { Success = false, Error = $"Wallet '{walletId}' did not receive enough fee funds" };
        }
        catch (Exception ex)
        {
            return new ActionResponse { Success = false, Error = ex.Message };
        }
    }

    private static async Task<ProjectCreatedResponse> DeployProjectAsync(
        MyProjectsViewModel myProjectsVm,
        Window window,
        string runId,
        string projectType)
    {
        // Click the Deploy button (DeployButton is shown on Step 6, NextStepButton is hidden)
        await ClickByNameAsync(window, "DeployButton");
        await Task.Delay(1000);

        // Wait for wallet list to load in deploy overlay
        var walletLoadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < walletLoadDeadline)
        {
            var ready = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                var deployVm = myProjectsVm.CreateProjectVm?.DeployFlow;
                var walletCount = deployVm?.Wallets?.Count ?? -1;
                return walletCount > 0;
            });
            if (ready)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        // Click the first WalletButton in the deploy overlay
        // Give ItemsControl time to materialize templates after wallet list populated
        await Task.Delay(1000);

        await ClickFirstWalletButtonAsync(window);

        // Click PayWithWalletButton — this triggers FeeSelectionPopup
        await ClickByNameAsync(window, "PayWithWalletButton");

        // FeeSelectionPopup appears — click ConfirmButton (defaults to "standard" 20 sat/vB)
        await ClickByNameAsync(window, "ConfirmButton", TimeSpan.FromSeconds(10));

        // Wait for deploy to complete — check PaymentFlowViewModel.IsSuccess since the
        // deploy modal is a PaymentFlowView (DeployFlowViewModel.CurrentScreen is not set
        // in the wallet-pay path)
        var deployDeadline = DateTime.UtcNow + TxTimeout;
        while (DateTime.UtcNow < deployDeadline)
        {
            var success = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                var paymentFlow = myProjectsVm.CreateProjectVm?.DeployFlow?.PaymentFlow;
                return paymentFlow?.IsSuccess == true;
            });
            if (success)
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        // Click SuccessActionButton in the PaymentFlowView success screen
        await ClickByNameAsync(window, "SuccessActionButton");
        await Task.Delay(500);

        var projectPollDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < projectPollDeadline)
        {
            var project = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await myProjectsVm.LoadFounderProjectsAsync();
                Dispatcher.UIThread.RunJobs();
                return myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId, StringComparison.Ordinal));
            });

            if (project != null)
            {
                return new ProjectCreatedResponse
                {
                    Success = true,
                    ProjectIdentifier = project.ProjectIdentifier,
                    OwnerWalletId = project.OwnerWalletId,
                    ProjectType = projectType,
                };
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        return new ProjectCreatedResponse { Success = false, Error = "Project did not appear after deploy" };
    }

    private static async Task OpenCreateWizardAsync(MyProjectsViewModel myProjectsVm, Window window)
    {
        // Try clicking the LaunchFromListButton (project list view) or the EmptyState "Launch a Project" button
        var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Try LaunchFromListButton first (visible when projects exist)
            var launchBtn = FindByName<Button>(window, "LaunchFromListButton");
            if (launchBtn is { IsVisible: true })
            {
                ClickButton(launchBtn);
                return true;
            }

            // Try MobileLaunchButton (compact mode)
            var mobileLaunchBtn = FindByName<Button>(window, "MobileLaunchButton");
            if (mobileLaunchBtn is { IsVisible: true })
            {
                ClickButton(mobileLaunchBtn);
                return true;
            }

            // EmptyState button — find by content text
            var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible);
            foreach (var btn in buttons)
            {
                if (btn.Content is string s && s == "Launch a Project")
                {
                    ClickButton(btn);
                    return true;
                }
                if (btn.Content is StackPanel sp)
                {
                    foreach (var child in sp.Children.OfType<TextBlock>())
                    {
                        if (child.Text == "Launch a Project")
                        {
                            ClickButton(btn);
                            return true;
                        }
                    }
                }
            }
            return false;
        });

        if (!clicked)
        {
            throw new InvalidOperationException("Could not find any 'Launch a Project' button");
        }

        await Task.Delay(500);
    }

    private static async Task CreateWalletViaGenerateAsync(Window window)
    {
        var addWalletBtn = await Dispatcher.UIThread.InvokeAsync(() => FindAddWalletButton(window));
        if (addWalletBtn == null)
        {
            throw new InvalidOperationException("Add Wallet button not found");
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ClickButton(addWalletBtn);
        });
        await Task.Delay(300);

        // Click Generate to create seed words
        await ClickWalletCardButtonAsync(window, "BtnGenerate");
        await Task.Delay(500);

        // Mark seed as downloaded (skips native file dialog) and click Continue
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var cwm = window.GetVisualDescendants().OfType<CreateWalletModal>().FirstOrDefault();
            cwm?.MarkSeedDownloaded();
            Dispatcher.UIThread.RunJobs();
        });
        await Task.Delay(200);

        // Click Continue Backup → triggers wallet creation via SDK
        await ClickWalletCardButtonAsync(window, "BtnContinueBackup");

        // Wait for success panel to appear
        var successDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < successDeadline)
        {
            var successVisible = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var panel = FindByAutomationId<Panel>(window, "CreateWalletSuccessPanel");
                return panel is { IsVisible: true };
            });

            if (successVisible) break;
            await Task.Delay(300);
        }

        // Click Done to close the modal
        await ClickWalletCardButtonAsync(window, "BtnCreateWalletDone");
        await Task.Delay(500);
    }

    private static async Task FundWalletViaFaucetAsync(Window window, FundsViewModel fundsVm, string walletId, string profileName)
    {
        var deadline = DateTime.UtcNow + FaucetTimeout;
        var faucetRetryInterval = TimeSpan.FromSeconds(30);
        var lastFaucetAttempt = DateTime.MinValue;

        while (DateTime.UtcNow < deadline)
        {
            var totalBalance = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                return fundsVm.TotalBalance;
            });

            if (totalBalance != "0.0000")
            {
                return;
            }

            if (DateTime.UtcNow - lastFaucetAttempt >= faucetRetryInterval)
            {
                lastFaucetAttempt = DateTime.UtcNow;
                Log(profileName, "Requesting faucet coins...");
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await fundsVm.GetTestCoinsAsync(walletId);
                    Dispatcher.UIThread.RunJobs();
                });
            }

            await ClickWalletCardButtonAsync(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Wallet balance did not become non-zero in time.");
    }

    private static async Task<InvestmentViewModel?> WaitForInvestmentAsync(
        PortfolioViewModel portfolioVm,
        string projectIdentifier,
        Func<InvestmentViewModel, bool> predicate)
    {
        var deadline = DateTime.UtcNow + IndexerLag;
        while (DateTime.UtcNow < deadline)
        {
            var match = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await portfolioVm.LoadInvestmentsFromSdkAsync();
                Dispatcher.UIThread.RunJobs();
                return portfolioVm.Investments.FirstOrDefault(i =>
                    string.Equals(i.ProjectIdentifier, projectIdentifier, StringComparison.Ordinal) && predicate(i));
            });

            if (match != null)
            {
                return match;
            }

            await Task.Delay(PollInterval);
        }

        return null;
    }

    private static async Task<MyProjectItemViewModel?> WaitForFounderProjectAsync(
        MyProjectsViewModel myProjectsVm,
        string projectIdentifier)
    {
        var deadline = DateTime.UtcNow + IndexerLag;
        while (DateTime.UtcNow < deadline)
        {
            var match = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await myProjectsVm.LoadFounderProjectsAsync();
                Dispatcher.UIThread.RunJobs();
                return myProjectsVm.Projects.FirstOrDefault(p =>
                    string.Equals(p.ProjectIdentifier, projectIdentifier, StringComparison.Ordinal));
            });

            if (match != null)
            {
                return match;
            }

            await Task.Delay(PollInterval);
        }

        return null;
    }

    private static async Task<bool> ClickManageProjectClaimStageAsync(
        Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project,
        int stageNumber)
    {
        // Click PART_ManageButton on the ProjectCard whose DataContext matches
        await ClickPartManageButtonAsync(window, project);

        // Wait for the stage claim button to appear and click it
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            var stageClicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var claimButton = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b =>
                    b.IsVisible && b.Classes.Contains("StageClaimBtn") && b.Tag is int tag && tag == stageNumber);
                if (claimButton == null) return false;
                ClickButton(claimButton);
                return true;
            });

            if (stageClicked) break;
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        // Select all UTXOs for the stage by toggling IsSelected on each item
        // The UTXO toggle is driven by PointerPressed on UtxoItemBorder, which sets
        // utxo.IsSelected and updates checkbox CSS class. We set IsSelected directly
        // since PointerPressedEventArgs can't be easily synthesized in Avalonia.
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var manageVm = myProjectsVm.SelectedManageProject;
            if (manageVm?.SelectedStage?.AvailableTransactions != null)
            {
                foreach (var tx in manageVm.SelectedStage.AvailableTransactions)
                {
                    tx.IsSelected = true;
                }
                Dispatcher.UIThread.RunJobs();
            }
        });
        await Task.Delay(200);

        // Click ClaimSelectedBtn — this triggers FeeSelectionPopup (async)
        await ClickByNameAsync(window, "ClaimSelectedBtn");

        // FeeSelectionPopup appears — click ConfirmButton (defaults to "standard" 20 sat/vB)
        await ClickByNameAsync(window, "ConfirmButton", TimeSpan.FromSeconds(10));

        // Wait for claim to complete (success modal or IsClaiming becomes false)
        var claimDeadline = DateTime.UtcNow + TxTimeout;
        while (DateTime.UtcNow < claimDeadline)
        {
            var completed = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                var manageVm = myProjectsVm.SelectedManageProject;
                return manageVm != null && (manageVm.ShowSuccessModal || !manageVm.IsClaiming);
            });

            if (completed) return true;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return false;
    }

    private static async Task<bool> ClickManageProjectReleaseFundsAsync(
        Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project)
    {
        // Click PART_ManageButton on the ProjectCard whose DataContext matches
        await ClickPartManageButtonAsync(window, project);

        // Click ReleaseFundsNavButton
        await ClickWalletCardButtonAsync(window, "ReleaseFundsNavButton");
        await Task.Delay(300);

        // Click ReleaseFundsConfirmBtn
        await ClickByNameAsync(window, "ReleaseFundsConfirmBtn");

        // Wait for release to complete
        var deadline = DateTime.UtcNow + TxTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var completed = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                var manageVm = myProjectsVm.SelectedManageProject;
                return manageVm != null && manageVm.ShowReleaseFundsSuccessModal;
            });

            if (completed) return true;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return false;
    }

    private static async Task NavigateToAsync(Window window, string section)
    {
        if (string.Equals(section, "Settings", StringComparison.OrdinalIgnoreCase))
        {
            // Click the SettingsButton in the desktop header
            await ClickByNameAsync(window, "SettingsButton");
        }
        else
        {
            // Find the Nav ListBox in the sidebar, then click the ListBoxItem whose NavItem.Label matches
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var sidebar = window.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(b => b.Name == "DesktopSidebar");
                if (sidebar == null)
                    throw new InvalidOperationException("DesktopSidebar not found");

                var navListBox = sidebar.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                if (navListBox == null)
                    throw new InvalidOperationException("Nav ListBox not found");

                // Find the NavItem in the items source
                var navItem = navListBox.ItemsSource?.Cast<object>()
                    .OfType<NavItem>()
                    .FirstOrDefault(n => string.Equals(n.Label, section, StringComparison.Ordinal));

                if (navItem == null)
                    throw new InvalidOperationException($"NavItem '{section}' not found");

                navListBox.SelectedItem = navItem;
                Dispatcher.UIThread.RunJobs();
            });
        }
        await Task.Delay(500);
    }

    private static async Task ClickWalletCardButtonAsync(Window window, string automationId)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var button = FindByAutomationId<Button>(window, automationId);
                if (button == null)
                {
                    return false;
                }

                ClickButton(button);
                return true;
            });

            if (clicked)
            {
                await Task.Delay(200);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"Button '{automationId}' not found within timeout");
    }

    private static async Task<Window> RequireWindowAsync()
    {
        var window = await Dispatcher.UIThread.InvokeAsync(GetMainWindow);
        return window ?? throw new InvalidOperationException("Main window not available");
    }

    private static Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private static ShellViewModel GetShellVm(Window window)
    {
        var shellView = window.GetVisualDescendants().OfType<ShellView>().FirstOrDefault();
        return shellView?.DataContext as ShellViewModel
            ?? throw new InvalidOperationException("ShellViewModel not found");
    }

    private static FundsViewModel? GetFundsViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<FundsView>().FirstOrDefault()?.DataContext as FundsViewModel;
    }

    private static MyProjectsViewModel? GetMyProjectsViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<MyProjectsView>().FirstOrDefault()?.DataContext as MyProjectsViewModel;
    }

    private static FindProjectsViewModel? GetFindProjectsViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<FindProjectsView>().FirstOrDefault()?.DataContext as FindProjectsViewModel;
    }

    private static FundersViewModel? GetFundersViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<FundersView>().FirstOrDefault()?.DataContext as FundersViewModel;
    }

    private static T? FindByAutomationId<T>(Visual root, string automationId) where T : Visual
    {
        return root.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);
    }

    private static T? FindByName<T>(Visual root, string name) where T : Control
    {
        return root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
    }

    private static Button? FindAddWalletButton(Window window)
    {
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible);
        foreach (var btn in buttons)
        {
            if (btn.Content is string text && text.Contains("Add Wallet", StringComparison.Ordinal))
            {
                return btn;
            }

            if (btn.Content is StackPanel panel)
            {
                foreach (var child in panel.Children.OfType<TextBlock>())
                {
                    if (child.Text == "Add Wallet")
                    {
                        return btn;
                    }
                }
            }
        }

        return null;
    }

    private static void ClickButton(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Try to click the ApproveButton with the matching Tag (signature Id) in the visual tree.
    /// Returns false if button not found (e.g. virtualized out of view).
    /// </summary>
    private static async Task<bool> ClickApproveButtonByIdAsync(Window window, int signatureId)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var approveButton = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b =>
                b.IsVisible && (b.Name == "ApproveButton" || b.Name == "MobileApproveButton")
                && b.Tag is int tag && tag == signatureId);

            if (approveButton == null) return false;
            ClickButton(approveButton);
            return true;
        });
    }

    /// <summary>
    /// Click the ManageButton whose Tag (InvestmentViewModel) matches the given projectIdentifier.
    /// </summary>
    private static async Task ClickManageButtonByProjectAsync(Window window, string projectIdentifier)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var buttons = window.GetVisualDescendants().OfType<Button>()
                    .Where(b => b.Name == "ManageButton" && b.IsVisible);
                foreach (var btn in buttons)
                {
                    if (btn.Tag is InvestmentViewModel inv &&
                        string.Equals(inv.ProjectIdentifier, projectIdentifier, StringComparison.Ordinal))
                    {
                        ClickButton(btn);
                        return true;
                    }
                }
                return false;
            });

            if (clicked)
            {
                await Task.Delay(200);
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"ManageButton for project '{projectIdentifier}' not found within timeout");
    }

    /// <summary>
    /// Click the FundPatternButton whose DataContext has the specified StageCount.
    /// </summary>
    private static async Task ClickFundPatternByStageCountAsync(Window window, int stageCount)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var buttons = window.GetVisualDescendants().OfType<Button>()
                    .Where(b => b.Name == "FundPatternButton" && b.IsVisible);
                foreach (var btn in buttons)
                {
                    if (btn.CommandParameter is FundingPatternOption opt && opt.StageCount == stageCount)
                    {
                        ClickButton(btn);
                        return true;
                    }
                }
                return false;
            });

            if (clicked)
            {
                await Task.Delay(200);
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"FundPatternButton with StageCount={stageCount} not found within timeout");
    }

    /// <summary>
    /// Click the first visible WalletButton in the payment flow modal.
    /// </summary>
    private static async Task ClickFirstWalletButtonAsync(Window window)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var (clicked, debugInfo) = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var allButtons = window.GetVisualDescendants().OfType<Button>().ToList();
                var walletBtns = allButtons.Where(b => b.Name == "WalletButton").ToList();
                var visibleWalletBtns = walletBtns.Where(b => b.IsVisible).ToList();
                var info = $"totalButtons={allButtons.Count}, namedWallet={walletBtns.Count}, visibleWallet={visibleWalletBtns.Count}";

                var walletBtn = visibleWalletBtns.FirstOrDefault();
                if (walletBtn == null) return (false, info);
                ClickButton(walletBtn);
                return (true, info);
            });

            if (clicked)
            {
                await Task.Delay(200);
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("WalletButton not found within timeout");
    }

    /// <summary>
    /// Click a Button found by Name (with optional wait+retry).
    /// Must be called from a background thread (dispatches to UI thread internally).
    /// </summary>
    private static async Task ClickByNameAsync(Window window, string name, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? UiTimeout);
        while (DateTime.UtcNow < deadline)
        {
            var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var button = FindByName<Button>(window, name)
                          ?? FindByAutomationId<Button>(window, name);
                if (button == null || !button.IsVisible || !button.IsEnabled) return false;
                ClickButton(button);
                return true;
            });

            if (clicked)
            {
                await Task.Delay(200);
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Button '{name}' not found/visible within timeout");
    }

    /// <summary>
    /// Set text on a TextBox found by Name.
    /// Must be called from a background thread.
    /// </summary>
    private static async Task TypeTextByNameAsync(Window window, string name, string text)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var textBox = FindByName<TextBox>(window, name)
                       ?? FindByAutomationId<TextBox>(window, name);
            if (textBox == null) throw new InvalidOperationException($"TextBox '{name}' not found");
            textBox.Text = text;
            Dispatcher.UIThread.RunJobs();
        });
        await Task.Delay(100);
    }

    /// <summary>
    /// Set value on a NumericUpDown found by Name.
    /// Must be called from a background thread.
    /// </summary>
    private static async Task SetNumericByNameAsync(Window window, string name, decimal value)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var nud = FindByName<NumericUpDown>(window, name)
                   ?? FindByAutomationId<NumericUpDown>(window, name);
            if (nud == null) throw new InvalidOperationException($"NumericUpDown '{name}' not found");
            nud.Value = value;
            Dispatcher.UIThread.RunJobs();
        });
        await Task.Delay(100);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Edit Project Profile
    // ═══════════════════════════════════════════════════════════════════

    public static async Task<EditProjectProfileResponse> EditProjectProfileAsync(
        IServiceProvider services,
        EditProjectProfileRequest request)
    {
        try
        {
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "My Projects");

            var myProjectsVm = await Dispatcher.UIThread.InvokeAsync(() => GetMyProjectsViewModel(window));
            if (myProjectsVm == null)
            {
                return new EditProjectProfileResponse { Success = false, Error = "MyProjectsViewModel not found" };
            }

            await Dispatcher.UIThread.InvokeAsync(async () => await myProjectsVm.LoadFounderProjectsAsync());
            await Task.Delay(500);

            var project = await Dispatcher.UIThread.InvokeAsync(() =>
                myProjectsVm.Projects.FirstOrDefault(p =>
                    string.Equals(p.ProjectIdentifier, request.ProjectIdentifier, StringComparison.Ordinal)));

            if (project == null)
            {
                return new EditProjectProfileResponse { Success = false, Error = $"Project '{request.ProjectIdentifier}' not found in My Projects" };
            }

            // Open edit profile by clicking PART_EditButton on the ProjectCard
            await ClickPartEditButtonAsync(window, project);
            await Task.Delay(1000); // Allow LoadAsync to fetch current profile from Nostr

            var editVm = await Dispatcher.UIThread.InvokeAsync(() => myProjectsVm.SelectedEditProject);
            if (editVm == null)
            {
                return new EditProjectProfileResponse { Success = false, Error = "EditProfileViewModel not opened" };
            }

            // Wait for loading to complete
            var loadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < loadDeadline)
            {
                var isLoading = await Dispatcher.UIThread.InvokeAsync(() => editVm.IsLoading);
                if (!isLoading) break;
                await Task.Delay(500);
            }

            // Apply requested changes via UI TextBox typing
            if (request.Name != null)
                await TypeTextByNameAsync(window, "ProfileNameTextBox", request.Name);
            if (request.DisplayName != null)
                await TypeTextByNameAsync(window, "ProfileDisplayNameTextBox", request.DisplayName);
            if (request.About != null)
                await TypeTextByNameAsync(window, "ProfileAboutTextBox", request.About);
            if (request.Picture != null)
                await TypeTextByNameAsync(window, "ProfilePictureTextBox", request.Picture);
            if (request.Banner != null)
                await TypeTextByNameAsync(window, "ProfileBannerTextBox", request.Banner);
            if (request.Website != null)
                await TypeTextByNameAsync(window, "ProfileWebsiteTextBox", request.Website);
            if (request.ProjectContent != null)
            {
                // Switch to Project tab first, then type into ProjectContentBox
                await ClickByNameAsync(window, "TabProject");
                await Task.Delay(300);
                await TypeTextByNameAsync(window, "ProjectContentBox", request.ProjectContent);
            }

            // Click SaveButton to save to Nostr
            await ClickByNameAsync(window, "SaveButton");

            // Wait for save to complete
            var saveDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < saveDeadline)
            {
                var isSaving = await Dispatcher.UIThread.InvokeAsync(() => editVm.IsSaving);
                if (!isSaving) break;
                await Task.Delay(300);
            }

            // Close edit profile by clicking EditBackButton
            await ClickByNameAsync(window, "EditBackButton");

            return new EditProjectProfileResponse { Success = true };
        }
        catch (Exception ex)
        {
            return new EditProjectProfileResponse { Success = false, Error = ex.ToString() };
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Fetch Project Profile (verify saved data)
    // ═══════════════════════════════════════════════════════════════════

    public static async Task<FetchProjectProfileResponse> FetchProjectProfileAsync(
        IServiceProvider services,
        FetchProjectProfileRequest request)
    {
        try
        {
            var projectAppService = services.GetRequiredService<Angor.Sdk.Funding.Projects.IProjectAppService>();
            var projectId = new ProjectId(request.ProjectIdentifier);

            var result = await projectAppService.FetchProjectProfileData(projectId);
            if (result.IsFailure)
            {
                return new FetchProjectProfileResponse { Success = false, Error = result.Error };
            }

            var data = result.Value;
            return new FetchProjectProfileResponse
            {
                Success = true,
                Name = data.Metadata?.Name,
                DisplayName = data.Metadata?.DisplayName,
                About = data.Metadata?.About,
                Picture = data.Metadata?.Picture,
                Banner = data.Metadata?.Banner,
                Website = data.Metadata?.Website,
                ProjectContent = data.ProjectContent,
            };
        }
        catch (Exception ex)
        {
            return new FetchProjectProfileResponse { Success = false, Error = ex.ToString() };
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Upload to Blossom (download image from URL, upload to blossom server)
    // ═══════════════════════════════════════════════════════════════════

    public static async Task<UploadToBlossomResponse> UploadToBlossomAsync(
        IServiceProvider services,
        UploadToBlossomRequest request)
    {
        try
        {
            var blossomService = services.GetRequiredService<BlossomUploadService>();

            // Download the image from the provided URL
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            var imageBytes = await httpClient.GetByteArrayAsync(request.ImageUrl);
            var contentType = "image/jpeg"; // default; picsum returns JPEG

            // Get Nostr key from the project's wallet for Blossom auth
            var window = await RequireWindowAsync();
            await NavigateToAsync(window, "My Projects");

            var myProjectsVm = await Dispatcher.UIThread.InvokeAsync(() => GetMyProjectsViewModel(window));
            if (myProjectsVm == null)
            {
                return new UploadToBlossomResponse { Success = false, Error = "MyProjectsViewModel not found" };
            }

            await Dispatcher.UIThread.InvokeAsync(async () => await myProjectsVm.LoadFounderProjectsAsync());
            await Task.Delay(500);

            var project = await Dispatcher.UIThread.InvokeAsync(() =>
                myProjectsVm.Projects.FirstOrDefault(p =>
                    string.Equals(p.ProjectIdentifier, request.ProjectIdentifier, StringComparison.Ordinal)));

            if (project == null)
            {
                return new UploadToBlossomResponse { Success = false, Error = $"Project '{request.ProjectIdentifier}' not found" };
            }

            // Use an ephemeral key for Blossom auth (simpler for testing)
            var nostrKeyHex = global::App.UI.Shared.Helpers.BlossomAuthKeyHelper.CreateEphemeralPrivateKeyHex();

            var uploadResult = await blossomService.UploadAsync(request.BlossomServer, imageBytes, contentType, nostrKeyHex);
            if (uploadResult.IsFailure)
            {
                return new UploadToBlossomResponse { Success = false, Error = uploadResult.Error };
            }

            return new UploadToBlossomResponse { Success = true, UploadedUrl = uploadResult.Value };
        }
        catch (Exception ex)
        {
            return new UploadToBlossomResponse { Success = false, Error = ex.ToString() };
        }
    }

    /// <summary>
    /// Set SelectedDate on a CalendarDatePicker found by Name.
    /// </summary>
    private static async Task SetCalendarDateByNameAsync(Window window, string name, DateTime date)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var picker = FindByName<CalendarDatePicker>(window, name)
                      ?? FindByAutomationId<CalendarDatePicker>(window, name);
            if (picker == null) throw new InvalidOperationException($"CalendarDatePicker '{name}' not found");
            picker.SelectedDate = date;
            Dispatcher.UIThread.RunJobs();
        });
        await Task.Delay(100);
    }

    /// <summary>
    /// Set SelectedItem on a ComboBox found by Name, matching by string value.
    /// </summary>
    private static async Task SetComboBoxByNameAsync(Window window, string name, string value)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var combo = FindByName<ComboBox>(window, name)
                     ?? FindByAutomationId<ComboBox>(window, name);
            if (combo == null) throw new InvalidOperationException($"ComboBox '{name}' not found");
            var item = combo.ItemsSource?.Cast<object>().FirstOrDefault(i =>
                string.Equals(i.ToString(), value, StringComparison.Ordinal));
            if (item != null)
                combo.SelectedItem = item;
            Dispatcher.UIThread.RunJobs();
        });
        await Task.Delay(100);
    }

    /// <summary>
    /// Click a ListBoxItem in a ListBox found by Name, matching by Tag value.
    /// </summary>
    private static async Task ClickListBoxItemByTagAsync(Window window, string listBoxName, string tagValue)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var listBox = FindByName<ListBox>(window, listBoxName)
                       ?? FindByAutomationId<ListBox>(window, listBoxName);
            if (listBox == null) throw new InvalidOperationException($"ListBox '{listBoxName}' not found");

            var items = listBox.GetVisualDescendants().OfType<ListBoxItem>().ToList();

            foreach (var item in items)
            {
                if (item.Tag is string tag && string.Equals(tag, tagValue, StringComparison.Ordinal))
                {
                    item.IsSelected = true;
                    listBox.SelectedItem = item;
                    Dispatcher.UIThread.RunJobs();
                    return;
                }
            }

            throw new InvalidOperationException($"ListBoxItem with Tag='{tagValue}' not found in '{listBoxName}'");
        });
        await Task.Delay(100);
    }

    /// <summary>
    /// Click PART_ManageButton on a ProjectCard whose DataContext matches the given project.
    /// </summary>
    private static async Task ClickPartManageButtonAsync(Window window, MyProjectItemViewModel project)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var cards = window.GetVisualDescendants().OfType<ProjectCard>().Where(c => c.IsVisible);
                foreach (var card in cards)
                {
                    if (card.DataContext is MyProjectItemViewModel vm &&
                        string.Equals(vm.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal))
                    {
                        var manageBtn = card.GetVisualDescendants().OfType<Button>()
                            .FirstOrDefault(b => b.Name == "PART_ManageButton" && b.IsVisible);
                        if (manageBtn != null)
                        {
                            ClickButton(manageBtn);
                            return true;
                        }
                    }
                }
                return false;
            });

            if (clicked)
            {
                await Task.Delay(500);
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"PART_ManageButton for project '{project.ProjectIdentifier}' not found within timeout");
    }

    /// <summary>
    /// Click PART_EditButton on a ProjectCard whose DataContext matches the given project.
    /// </summary>
    private static async Task ClickPartEditButtonAsync(Window window, MyProjectItemViewModel project)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var cards = window.GetVisualDescendants().OfType<ProjectCard>().Where(c => c.IsVisible);
                foreach (var card in cards)
                {
                    if (card.DataContext is MyProjectItemViewModel vm &&
                        string.Equals(vm.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal))
                    {
                        var editBtn = card.GetVisualDescendants().OfType<Button>()
                            .FirstOrDefault(b => b.Name == "PART_EditButton" && b.IsVisible);
                        if (editBtn != null)
                        {
                            ClickButton(editBtn);
                            return true;
                        }
                    }
                }
                return false;
            });

            if (clicked)
            {
                await Task.Delay(500);
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"PART_EditButton for project '{project.ProjectIdentifier}' not found within timeout");
    }

    private static void Log(string ctx, string msg)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{ctx}] {msg}");
    }
}

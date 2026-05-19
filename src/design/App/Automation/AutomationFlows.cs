using System;
using System.Globalization;
using System.Reflection;
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

            await OpenCreateWizardAsync(myProjectsVm);

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

            return await DeployProjectAsync(myProjectsVm, req.RunId, "fund");
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

            await OpenCreateWizardAsync(myProjectsVm);

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

            // Step 4: Target amount + invest end date (date picker set via VM)
            await TypeTextByNameAsync(window, "InvestTargetAmountInput", "1.0");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var wizardVm = myProjectsVm.CreateProjectVm;
                wizardVm.InvestEndDate = DateTime.UtcNow.AddMonths(3);
                Dispatcher.UIThread.RunJobs();
            });
            await ClickByNameAsync(window, "NextStepButton");

            // Step 5 interstitial: Dismiss welcome
            await ClickByNameAsync(window, "Step5WelcomeButton");
            await Task.Delay(200);

            // Step 5: Duration + frequency + start date + generate stages
            await TypeTextByNameAsync(window, "DurationValueInput", "3");
            // ComboBox and ListBox selection + StartDate set via VM (no simple click target)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var wizardVm = myProjectsVm.CreateProjectVm;
                wizardVm.DurationUnit = "Months";
                wizardVm.ReleaseFrequency = "Monthly";
                wizardVm.StartDate = DateTime.UtcNow.AddDays(-120).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Dispatcher.UIThread.RunJobs();
            });

            await ClickByNameAsync(window, "GenerateStagesButton");
            await ClickByNameAsync(window, "NextStepButton");

            return await DeployProjectAsync(myProjectsVm, req.RunId, "investment");
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

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                findProjectsVm.OpenProjectDetail(foundProject);
                Dispatcher.UIThread.RunJobs();
            });
            await Task.Delay(300);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                findProjectsVm.OpenInvestPage();
                Dispatcher.UIThread.RunJobs();
            });
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

            if (req.TargetPatternStageCount > 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var investPageView = window.GetVisualDescendants().OfType<InvestPageView>().FirstOrDefault();
                    var targetBorder = investPageView?.GetVisualDescendants()
                        .OfType<Border>()
                        .FirstOrDefault(b => b.Name == "FundPatternBorder"
                            && b.DataContext is FundingPatternOption opt
                            && opt.StageCount == req.TargetPatternStageCount);

                    if (targetBorder?.DataContext is FundingPatternOption option)
                    {
                        investVm.SelectFundingPattern(option);
                        Dispatcher.UIThread.RunJobs();
                    }
                });
                await Task.Delay(300);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                investVm.InvestmentAmount = req.AmountBtc;
                investVm.Submit();
                Dispatcher.UIThread.RunJobs();
            });

            var maxPayAttempts = 3;
            for (var payAttempt = 1; payAttempt <= maxPayAttempts; payAttempt++)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var wallet = investVm.Wallets[0];
                    investVm.PaymentFlow.SelectWallet(wallet);
                    Dispatcher.UIThread.RunJobs();
                    investVm.PaymentFlow.PayWithWalletCommand.Execute().Subscribe();
                });

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

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                investVm.AddToPortfolio();
                Dispatcher.UIThread.RunJobs();
            });

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
                var approved = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var method = typeof(FundersViewModel).GetMethod(
                        "ApproveSignatureAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (method == null) return false;
                    var task = (Task?)method.Invoke(fundersVm, new object[] { pendingSignature });
                    if (task != null) await task;
                    return true;
                });

                if (!approved)
                {
                    return new ApproveInvestmentsResponse
                    {
                        Success = false,
                        Error = $"ApproveSignatureAsync method not found for signature {pendingSignature.Id}",
                    };
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
            var investment = await WaitForInvestmentAsync(portfolioVm, req.ProjectIdentifier, i => i.Step >= 2);
            if (investment == null)
            {
                return new ConfirmInvestmentResponse { Success = false, Error = "Approved investment not found" };
            }

            var result = await Dispatcher.UIThread.InvokeAsync(async () =>
                await portfolioVm.ConfirmInvestmentAsync(investment));
            _ = result;

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
                    myProjectsVm.OpenManageProject(founderProject);
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

                await Task.Delay(PollInterval);
            }

            var clicked = await ClickManageProjectClaimStageAsync(window, myProjectsVm, founderProject, req.StageNumber);
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
            }

            var deadline = DateTime.UtcNow + IndexerLag;
            while (DateTime.UtcNow < deadline)
            {
                var actionKey = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await portfolioVm.LoadRecoveryStatusAsync(investment);
                    Dispatcher.UIThread.RunJobs();
                    return investment.RecoveryState.ActionKey;
                });

                if (string.Equals(actionKey, req.Action, StringComparison.OrdinalIgnoreCase)
                    || (req.Action == "belowThreshold" && actionKey == "belowThreshold")
                    || (req.Action == "unfundedRelease" && actionKey == "unfundedRelease"))
                {
                    break;
                }

                await Task.Delay(PollInterval);
            }

            var succeeded = false;
            var attemptDeadline = DateTime.UtcNow + IndexerLag;
            while (DateTime.UtcNow < attemptDeadline && !succeeded)
            {
                succeeded = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    return req.Action switch
                    {
                        "recovery" => (await portfolioVm.RecoverFundsAsync(investment)).Success,
                        "penaltyRelease" => (await portfolioVm.PenaltyReleaseFundsAsync(investment)).Success,
                        "belowThreshold" => (await portfolioVm.ClaimEndOfProjectAsync(investment)).Success,
                        "unfundedRelease" => (await portfolioVm.ReleaseFundsAsync(investment)).Success,
                        _ => false,
                    };
                });

                if (!succeeded)
                {
                    await Task.Delay(PollInterval);
                }
            }

            return succeeded
                ? new RecoveryResponse { Success = true, ActionKey = req.Action }
                : new RecoveryResponse { Success = false, Error = $"Recovery action '{req.Action}' failed" };
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
        string runId,
        string projectType)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            myProjectsVm.CreateProjectVm.Deploy();
            Dispatcher.UIThread.RunJobs();
        });
        await Task.Delay(1000);

        var deployVm = await Dispatcher.UIThread.InvokeAsync(() => myProjectsVm.CreateProjectVm.DeployFlow);
        var walletLoadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < walletLoadDeadline)
        {
            var ready = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                return deployVm.Wallets.Count > 0;
            });
            if (ready)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            deployVm.SelectWallet(deployVm.Wallets[0]);
            Dispatcher.UIThread.RunJobs();
            deployVm.PayWithWallet();
        });

        var deployDeadline = DateTime.UtcNow + TxTimeout;
        while (DateTime.UtcNow < deployDeadline)
        {
            var success = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                return deployVm.CurrentScreen == DeployScreen.Success;
            });
            if (success)
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            deployVm.GoToMyProjects();
            Dispatcher.UIThread.RunJobs();
        });
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

    private static async Task OpenCreateWizardAsync(MyProjectsViewModel myProjectsVm)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            myProjectsVm.CreateProjectVm.ResetWizard();
            myProjectsVm.LaunchCreateWizard();
            Dispatcher.UIThread.RunJobs();
            myProjectsVm.CreateProjectVm.OnProjectDeployed = () =>
            {
                myProjectsVm.OnProjectDeployed(myProjectsVm.CreateProjectVm);
                myProjectsVm.CloseCreateWizard();
            };
        });
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

    private static async Task<bool> ClickApproveSignatureAsync(Window window, int signatureId)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var clicked = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var approveButton = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b =>
                    b.IsVisible && b.Name == "ApproveButton" && b.Tag is int tag && tag == signatureId);

                if (approveButton == null)
                {
                    return false;
                }

                ClickButton(approveButton);
                return true;
            });

            if (clicked)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return false;
    }

    private static async Task<bool> ClickManageProjectClaimStageAsync(
        Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project,
        int stageNumber)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            myProjectsVm.OpenManageProject(project);
            Dispatcher.UIThread.RunJobs();
        });

        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            var completed = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var manageVm = myProjectsVm.SelectedManageProject;
                if (manageVm == null)
                {
                    return false;
                }

                var claimButton = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b =>
                    b.IsVisible && b.Classes.Contains("StageClaimBtn") && b.Tag is int tag && tag == stageNumber);
                if (claimButton == null)
                {
                    return false;
                }

                ClickButton(claimButton);

                var claimSelected = FindByName<Button>(window, "ClaimSelectedBtn");
                if (claimSelected == null || manageVm.SelectedStage?.AvailableTransactions.Count <= 0)
                {
                    return false;
                }

                foreach (var tx in manageVm.SelectedStage.AvailableTransactions)
                {
                    tx.IsSelected = true;
                }

                Dispatcher.UIThread.RunJobs();
                ClickButton(claimSelected);

                var feeConfirmButton = FindByAutomationId<Button>(window, "FeeConfirmButton");
                if (feeConfirmButton == null)
                {
                    return false;
                }

                ClickButton(feeConfirmButton);
                return !manageVm.IsClaiming || manageVm.ShowSuccessModal;
            });

            if (completed)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        return false;
    }

    private static async Task<bool> ClickManageProjectReleaseFundsAsync(
        Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            myProjectsVm.OpenManageProject(project);
            Dispatcher.UIThread.RunJobs();
        });

        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var completed = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var manageVm = myProjectsVm.SelectedManageProject;
                if (manageVm == null)
                {
                    return false;
                }

                var releaseNav = FindByAutomationId<Button>(window, "ReleaseFundsNavButton");
                if (releaseNav == null || !releaseNav.IsVisible)
                {
                    return false;
                }

                ClickButton(releaseNav);
                var confirm = FindByName<Button>(window, "ReleaseFundsConfirmBtn");
                if (confirm == null || !confirm.IsVisible)
                {
                    return false;
                }

                ClickButton(confirm);
                return !manageVm.IsReleasingFunds || manageVm.ShowReleaseFundsSuccessModal;
            });

            if (completed)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        return false;
    }

    private static async Task NavigateToAsync(Window window, string section)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = GetShellVm(window);
            if (string.Equals(section, "Settings", StringComparison.OrdinalIgnoreCase))
            {
                vm.NavigateToSettings();
            }
            else
            {
                var navItem = vm.NavEntries.OfType<NavItem>().First(n => n.Label == section);
                vm.SelectedNavItem = navItem;
            }

            Dispatcher.UIThread.RunJobs();
        });
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

            // Open edit profile
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                myProjectsVm.OpenEditProfile(project);
                Dispatcher.UIThread.RunJobs();
            });
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

            // Apply requested changes
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (request.Name != null) editVm.ProfileName = request.Name;
                if (request.DisplayName != null) editVm.ProfileDisplayName = request.DisplayName;
                if (request.About != null) editVm.ProfileAbout = request.About;
                if (request.Picture != null) editVm.ProfilePicture = request.Picture;
                if (request.Banner != null) editVm.ProfileBanner = request.Banner;
                if (request.Website != null) editVm.ProfileWebsite = request.Website;
                if (request.ProjectContent != null) editVm.ProjectContent = request.ProjectContent;
                Dispatcher.UIThread.RunJobs();
            });

            // Save to Nostr (must invoke on UI thread as SaveAsync triggers toast UI)
            var saveResult = await Dispatcher.UIThread.InvokeAsync(async () => await editVm.SaveAsync());
            if (!saveResult)
            {
                return new EditProjectProfileResponse { Success = false, Error = "SaveAsync returned false" };
            }

            // Close edit profile
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                myProjectsVm.CloseEditProfile();
                Dispatcher.UIThread.RunJobs();
            });

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

    private static void Log(string ctx, string msg)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{ctx}] {msg}");
    }
}

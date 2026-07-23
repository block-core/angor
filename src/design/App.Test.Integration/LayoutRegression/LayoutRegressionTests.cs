using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funders;
using App.UI.Sections.Funds;
using App.UI.Sections.Home;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.EditProfile;
using App.UI.Sections.Portfolio;
using App.UI.Sections.Settings;
using App.UI.Shared;
using App.UI.Shared.PaymentFlow;
using App.UI.Shared.Services;
using Angor.Sdk.Common;
using App.UI.Shared.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Layout-regression tests: render key views headlessly at the responsive
/// breakpoint widths (and the off-by-one edge widths where responsive bugs
/// live), then walk the visual tree asserting no sibling overlaps and no
/// children overflowing their parent panels.
///
/// These catch the "button overlays the title on mobile" class of bug
/// (e.g. the Recover button overlapping "Payment Schedule", the claimable
/// badge overlaying stage headers) without brittle pixel snapshots.
///
/// View models are fabricated with worst-case data (long names, many stages,
/// all badges visible) — no network access, so these are fast and deterministic.
/// </summary>
public class LayoutRegressionTests
{
    /// <summary>Phone, phone-landscape/edge, tablet edge, tablet, desktop edge, desktop.</summary>
    public static TheoryData<double, double> Viewports => new()
    {
        { 360, 800 },   // small phone
        { 390, 844 },   // typical phone
        { 767, 1024 },  // 1px below tablet breakpoint
        { 768, 1024 },  // tablet breakpoint
        { 1023, 768 },  // 1px below desktop breakpoint
        { 1280, 800 },  // desktop
    };

    // ═══════════════════════════════════════════════════════════════════
    // InvestmentDetailView — Recover button / info cards / stages table
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void InvestmentDetailView_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = CreateWorstCaseInvestment();
        var view = new InvestmentDetailView { DataContext = vm };

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"InvestmentDetailView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ManageProjectContentView — stage cards, claim buttons, badges
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void ManageProjectContentView_has_no_overlaps_or_overflow(double width, double height)
    {
        var factory = global::App.App.Services
            .GetRequiredService<Func<MyProjectItemViewModel, ManageProjectViewModel>>();
        var vm = factory(new MyProjectItemViewModel
        {
            Name = "A Very Long Project Name That Should Wrap Not Overlay Anything",
            Description = new string('x', 200),
            ProjectType = "fund",
            TargetAmount = "0.50000",
            ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
        });

        // Worst case: stages with every badge/button state visible at once.
        vm.Stages.Add(new ManageStageViewModel
        {
            Number = 1, AmountLeft = "0.12345678", UtxoCount = 12,
            CompletionDate = "01 Jan 2027", Available = true, CanClaim = true,
            UnspentTransactionCount = 3, ClaimableTransactionCount = 2, TotalTransactionCount = 5,
            SpentTransactionCount = 2,
        });
        vm.Stages.Add(new ManageStageViewModel
        {
            Number = 2, AmountLeft = "0.00000000", UtxoCount = 4,
            CompletionDate = "01 Mar 2027", Available = true,
            SpentTransactionCount = 4, TotalTransactionCount = 4,
        });
        vm.Stages.Add(new ManageStageViewModel
        {
            Number = 3, AmountLeft = "0.25000000", UtxoCount = 8,
            CompletionDate = "01 Jun 2027", Available = false,
            DaysUntilAvailable = 42,
            UnspentTransactionCount = 8, TotalTransactionCount = 8,
        });

        var view = new ManageProjectContentView { DataContext = vm };

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"ManageProjectContentView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FundersView — empty states + tabs
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void FundersView_empty_state_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = global::App.App.Services.GetRequiredService<FundersViewModel>();
        var view = new FundersView(vm);

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"FundersView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ProjectCard — long content + stats-pending state
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [InlineData(320, false)]
    [InlineData(320, true)]
    [InlineData(390, false)]
    [InlineData(390, true)]
    public void ProjectCard_has_no_overlaps_or_overflow(double width, bool statsPending)
    {
        var card = new ProjectCard
        {
            ProjectName = "An Extremely Long Project Name That Must Trim Not Break Layout",
            ShortDescription = new string('y', 300),
            InvestorCount = 123456,
            InvestorLabel = "Funders",
            Raised = "21000000.00000000",
            Target = "21000000.00000",
            TargetLabel = "Goal:",
            Progress = 100,
            ProjectType = "Fund",
            Status = "Funding Closed",
            ShowManageFunds = true,
            StatsPending = statsPending,
        };

        var violations = RenderAndAudit(card, width, 900);

        violations.Should().BeEmpty(
            $"ProjectCard must not have overlapping/overflowing elements at width {width} (statsPending={statsPending}):\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // HomeView — static hero/cards content, responsive grid restructure
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void HomeView_has_no_overlaps_or_overflow(double width, double height)
    {
        var view = new HomeView(new HomeViewModel());

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"HomeView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FindProjectsView — card grid + search strip + load-more footer
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void FindProjectsView_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = global::App.App.Services.GetRequiredService<FindProjectsViewModel>();
        // Deterministic worst case: skip the async relay load's UI states and
        // populate the grid ourselves (long names, error strip, load-more visible).
        vm.IsInitialLoad = false;
        vm.IsLoading = false;
        vm.HasMoreItems = true;
        vm.SearchError = "Search failed: this is a fairly long error message that must wrap on phones";
        vm.Projects.Clear();
        for (int i = 0; i < 4; i++)
        {
            vm.Projects.Add(CreateWorstCaseProjectItem());
        }

        var view = new FindProjectsView(vm);

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"FindProjectsView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ProjectDetailView — header, stats, FAQ/members/media accordions
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void ProjectDetailView_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = CreateWorstCaseProjectItem();
        var view = new ProjectDetailView { DataContext = vm };

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"ProjectDetailView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // InvestPageView — amount entry, quick amounts, stage breakdown
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void InvestPageView_has_no_overlaps_or_overflow(double width, double height)
    {
        var factory = global::App.App.Services
            .GetRequiredService<Func<ProjectItemViewModel, InvestPageViewModel>>();
        var vm = factory(CreateWorstCaseProjectItem());
        vm.InvestmentAmount = "0.12345678";

        var view = new InvestPageView { DataContext = vm };

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"InvestPageView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // PortfolioView — investment cards, totals, penalties
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void PortfolioView_has_no_overlaps_or_overflow(double width, double height)
    {
        // PortfolioViewModel is a process-wide singleton: populate deterministically
        // and restore the empty state afterwards so other tests aren't affected.
        var vm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        vm.Investments.Clear();
        vm.Investments.Add(CreateWorstCaseInvestment());
        vm.Investments.Add(CreateWorstCaseInvestment());
        vm.HasInvestments = true;
        vm.IsLoading = false;

        try
        {
            var view = new PortfolioView(vm);

            var violations = RenderAndAudit(view, width, height);

            violations.Should().BeEmpty(
                $"PortfolioView must not have overlapping/overflowing elements at {width}x{height}:\n" +
                string.Join("\n", violations));
        }
        finally
        {
            vm.Investments.Clear();
            vm.HasInvestments = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // FundsView — wallet empty state + stats cards
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void FundsView_empty_state_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = global::App.App.Services.GetRequiredService<FundsViewModel>();
        var view = new FundsView(vm);

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"FundsView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // SettingsView — indexer/relay/explorer lists, network card
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void SettingsView_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = global::App.App.Services.GetRequiredService<SettingsViewModel>();
        var view = new SettingsView(vm);

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"SettingsView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CreateProjectView — all 6 wizard steps at phone + desktop widths
    // ═══════════════════════════════════════════════════════════════════

    public static TheoryData<int, double> CreateProjectSteps
    {
        get
        {
            var data = new TheoryData<int, double>();
            for (int step = 1; step <= 6; step++)
            {
                data.Add(step, 360);
                data.Add(step, 768);
                data.Add(step, 1280);
            }

            return data;
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(CreateProjectSteps))]
    public void CreateProjectView_step_has_no_overlaps_or_overflow(int step, double width)
    {
        var vm = global::App.App.Services.GetRequiredService<CreateProjectViewModel>();
        vm.SelectProjectType("fund");
        vm.ProjectName = "A Very Long Project Name That Stresses The Wizard Header Layout";
        vm.ProjectAbout = new string('x', 240);
        vm.GoToStep(step);

        var view = new CreateProjectView { DataContext = vm };

        var violations = RenderAndAudit(view, width, 900);

        violations.Should().BeEmpty(
            $"CreateProjectView step {step} must not have overlapping/overflowing elements at width {width}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // PaymentFlowView — "Use an existing wallet" modal (issue #920:
    // Fund Deployment label overlapped by the amount; wallet card cramping)
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void PaymentFlowView_wallet_selector_has_no_overlaps_or_overflow(double width, double height)
    {
        var services = global::App.App.Services;
        var config = new PaymentFlowConfig
        {
            AmountSats = 100_000_000, // 1.00000000 — worst-case width amount
            Title = "Fund Deployment",
            SuccessTitle = "Deployed",
            SuccessButtonText = "Go to My Projects",
            OnSuccessButtonClicked = () => { },
            OnPaymentReceived = (_, _, _) => Task.FromResult(CSharpFunctionalExtensions.Result.Success()),
        };
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("PaymentFlowLayoutTest");
        var vm = ActivatorUtilities.CreateInstance<PaymentFlowViewModel>(
            services, config, logger);

        var view = new PaymentFlowView { DataContext = vm };

        // Wallet context is empty in tests: exercise the wallet-card template with
        // worst-case fabricated wallets (long name, long balance, pending balance).
        var w1 = new WalletInfo(
            new WalletId("test-1"),
            "A Very Long Wallet Name That Must Not Overlap The Balance", "TBTC")
        {
            TotalBalanceSats = 123_456_789,
            UnconfirmedBalanceSats = 12_345_678,
            IsSelected = true,
        };
        var w2 = new WalletInfo(
            new WalletId("test-2"), "Angor Wallet", "TBTC")
        {
            TotalBalanceSats = 0,
        };
        view.AttachedToVisualTree += (_, _) =>
        {
            if (view.FindControl<ItemsControl>("WalletsList") is { } list)
                list.ItemsSource = new[] { w1, w2 };
        };

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"PaymentFlowView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // InvestorBreakdownView — optimistic-loading modal (table→cards on mobile)
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void InvestorBreakdownView_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = new InvestorBreakdownViewModel(
            "A Very Long Project Name That Stresses The Modal Header Layout",
            "fund", "TBTC",
            "aaaa000000000000000000000000000000000000000000000000000000000000");

        // Worst-case rows: current-user highlight + long amounts.
        var rows = new System.Collections.Generic.List<Angor.Sdk.Funding.Projects.Dtos.InvestorShareDto>();
        for (int i = 0; i < 6; i++)
        {
            rows.Add(new Angor.Sdk.Funding.Projects.Dtos.InvestorShareDto(
                i == 2
                    ? "aaaa000000000000000000000000000000000000000000000000000000000000"
                    : $"bbbb{i:D60}",
                "", 123_456_789, 33.33, 12_345_678, 10.01));
        }
        vm.ApplyData(new Angor.Sdk.Funding.Projects.Operations.GetInvestorShares.GetInvestorSharesResponse(
            370_370_367, rows.Count, rows));

        var view = new InvestorBreakdownView { DataContext = vm };
        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"InvestorBreakdownView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void InvestorBreakdownView_loading_state_has_no_overlaps_or_overflow(double width, double height)
    {
        var vm = new InvestorBreakdownViewModel("Project", "fund", "TBTC");

        var view = new InvestorBreakdownView { DataContext = vm };
        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"InvestorBreakdownView (loading) must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // EditProfileView — tabs (faq/members/media/relays) with long content
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaTheory]
    [MemberData(nameof(Viewports))]
    public void EditProfileView_has_no_overlaps_or_overflow(double width, double height)
    {
        var factory = global::App.App.Services
            .GetRequiredService<Func<MyProjectItemViewModel, EditProfileViewModel>>();
        // Bogus identifier: the VM's background profile fetch fails fast, and we
        // populate the fields we care about deterministically here.
        var vm = factory(new MyProjectItemViewModel
        {
            Name = "A Very Long Project Name That Should Wrap Not Overlay Anything",
            Description = new string('x', 200),
            ProjectType = "fund",
            ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
        });

        var view = new EditProfileView { DataContext = vm };

        var violations = RenderAndAudit(view, width, height);

        violations.Should().BeEmpty(
            $"EditProfileView must not have overlapping/overflowing elements at {width}x{height}:\n" +
            string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Harness
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hosts the control in a window at the given viewport, drives the
    /// responsive LayoutModeService the same way ShellView does, flushes
    /// layout, and returns any layout violations found.
    /// </summary>
    private static List<string> RenderAndAudit(Control view, double width, double height)
    {
        // Drive the responsive singleton BEFORE construction-sensitive layout runs.
        LayoutModeService.Instance.UpdateWidth(width);

        var window = new Window
        {
            Width = width,
            Height = height,
            SizeToContent = SizeToContent.Manual,
            // Mirror the real shell: sections live inside scrollable content,
            // so vertical overflow of the window is fine — horizontal is not.
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = view,
            },
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            // Responsive handlers (IsCompact subscriptions) may mutate grids after
            // the first pass — run a second layout to settle.
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            return LayoutAsserts.FindViolations(view);
        }
        finally
        {
            window.Close();
            // Reset process-global responsive state so it can't leak between tests.
            LayoutModeService.Instance.UpdateWidth(1280);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Worst-case investment VM: fund-type, recover button visible, many stages, long strings.</summary>
    private static InvestmentViewModel CreateWorstCaseInvestment()
    {
        var stages = new ObservableCollection<InvestmentStageViewModel>();
        for (int i = 1; i <= 8; i++)
        {
            stages.Add(new InvestmentStageViewModel
            {
                StageNumber = i,
                Percentage = "12.5%",
                ReleaseDate = "01 Jan 2027",
                Amount = "0.06250000",
                Status = i % 3 == 0 ? "Recovered (In Penalty)" : i % 2 == 0 ? "Released" : "Not Spent",
                StagePrefix = "Payment",
            });
        }

        return new InvestmentViewModel
        {
            ProjectName = "A Very Long Project Name That Stresses The Header Layout Badly",
            ShortDescription = new string('z', 250),
            TotalInvested = "0.04740000",
            FundingAmount = "0.0474 TBTC",
            FundingDate = "16 Jul 2026",
            StartDate = "09 Jul 2026",
            EndDate = "Open Ended",
            TransactionDate = "16 Jul 2026",
            TargetAmount = "0.5000",
            TotalRaised = "0.0474",
            TotalInvestors = 12345,
            Progress = 9.48,
            Status = "Active",
            ProjectType = "fund",
            Step = 3,
            ApprovalStatus = "Approved",
            // Longest button label: "Recover without Penalty"
            RecoveryState = new RecoveryState(
                HasUnspentItems: true,
                HasSpendableItemsInPenalty: false,
                HasReleaseSignatures: true,
                EndOfProject: false,
                IsAboveThreshold: true),
            Stages = stages,
            InvestmentTransactionId = new string('a', 64),
            ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
        };
    }

    /// <summary>
    /// Worst-case project item POCO: fund-type, long strings, huge stats,
    /// populated FAQ/members/media accordions, many dynamic stages.
    /// No image URLs so nothing hits the network.
    /// </summary>
    private static ProjectItemViewModel CreateWorstCaseProjectItem()
    {
        var item = new ProjectItemViewModel
        {
            ProjectName = "An Extremely Long Project Name That Must Wrap Or Trim Not Break Layout",
            ShortDescription = new string('y', 300),
            Description = new string('d', 500),
            ProfileDescription = new string('p', 400),
            ProfileLoaded = true,
            InvestorCount = 123456,
            InvestorLabel = "Funders",
            Raised = "21000000.00000000",
            Target = "21000000.00000",
            TargetLabel = "Goal:",
            Progress = 100,
            ProjectType = "Fund",
            Status = "Funding Closed",
            ProjectId = "angor1qtest000000000000000000000000000000000",
            FounderKey = new string('f', 64),
            NostrNpub = "npub1" + new string('n', 58),
            StartDate = "09 Jul 2026",
            EndDate = "Open Ended",
            ExpiryDate = "01 Jan 2030",
            PenaltyDays = "90",
        };

        for (int i = 1; i <= 5; i++)
        {
            item.FaqItems.Add(new ProjectFaqItemViewModel
            {
                Question = $"Question {i}: a fairly long question line that must wrap on phone widths?",
                Answer = new string('a', 220),
            });
            item.MemberPubkeys.Add("npub1" + new string((char)('a' + i), 58));
            item.MediaItems.Add(new ProjectMediaItemViewModel { Url = "", Type = "image" });
        }

        for (int i = 1; i <= 8; i++)
        {
            item.Stages.Add(new InvestmentStageViewModel
            {
                StageNumber = i,
                Percentage = "12.5%",
                ReleaseDate = "01 Jan 2027",
                Amount = "0.06250000",
                Status = "Not Spent",
                StagePrefix = "Payment",
            });
        }

        return item;
    }
}

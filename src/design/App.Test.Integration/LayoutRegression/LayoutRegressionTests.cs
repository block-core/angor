using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using App.Test.Integration.Helpers;
using App.UI.Sections.Funders;
using App.UI.Sections.MyProjects;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
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
}

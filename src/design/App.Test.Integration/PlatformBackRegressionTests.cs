using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using App.UI.Sections.FindProjects;
using App.UI.Sections.MyProjects;
using App.UI.Sections.Portfolio;
using App.UI.Sections.Settings;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Xunit;

namespace App.Test.Integration;

/// <summary>
/// Regression tests for the Android physical/system back button routing.
///
/// The physical back button has repeatedly broken (commits 39350f93, 0300e890,
/// 79bc736b, plus follow-up touches) because the shell derives "can go back"
/// from booleans reverse-engineered out of the string-keyed view cache
/// (SyncDetailStateFromCachedViews) and a hand-maintained priority ladder
/// (TryHandlePlatformBack). Any new overlay/detail screen — or a renamed nav
/// label — silently breaks back navigation.
///
/// These tests exercise the exact ShellViewModel entry points MainActivity
/// calls (CanHandlePlatformBack / TryHandlePlatformBack) headlessly, for every
/// state in the ladder and for the priority ordering between them. If you add
/// a new overlay/detail surface, add a case here.
/// </summary>
public class PlatformBackRegressionTests
{
    private static ShellViewModel GetShell()
    {
        var shell = global::App.App.Services.GetRequiredService<ShellViewModel>();
        // Make sure the section views the back ladder inspects exist in the cache,
        // exactly as they would after real navigation. These keys are load-bearing:
        // SyncDetailStateFromCachedViews pattern-matches on them. If a nav label is
        // renamed without updating the back ladder, these tests fail loudly instead
        // of the back button silently dying on device.
        shell.EnsureViewCreated("Find Projects");
        shell.EnsureViewCreated("Funded");
        shell.EnsureViewCreated("My Projects");
        shell.EnsureViewCreated("Settings");
        return shell;
    }

    private static FindProjectsViewModel FindProjectsVm(ShellViewModel shell) =>
        (FindProjectsViewModel)((Control)shell.ViewCache["Find Projects"]).DataContext!;

    private static MyProjectsViewModel MyProjectsVm(ShellViewModel shell) =>
        (MyProjectsViewModel)((Control)shell.ViewCache["My Projects"]).DataContext!;

    private static SettingsViewModel SettingsVm(ShellViewModel shell) =>
        (SettingsViewModel)((Control)shell.ViewCache["Settings"]).DataContext!;

    private static PortfolioViewModel PortfolioVm() =>
        global::App.App.Services.GetRequiredService<PortfolioViewModel>();

    /// <summary>Returns the shell to a root state so tests can't leak into each other.</summary>
    private static void ResetToRoot(ShellViewModel shell)
    {
        shell.HideModal();
        FindProjectsVm(shell).CloseInvestPage();
        FindProjectsVm(shell).CloseProjectDetail();
        PortfolioVm().CloseInvestmentDetail();
        var mp = MyProjectsVm(shell);
        mp.CancelCreateWizard();
        mp.CloseEditProfile();
        mp.CloseManageProject();
        SettingsVm(shell).CloseNetworkModal();
        SettingsVm(shell).CloseWipeDataModal();
        shell.IsSettingsOpen = false;
        shell.SyncDetailStateFromCachedViews();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Root state
    // ─────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void At_root_back_is_not_handled_so_android_may_exit()
    {
        var shell = GetShell();
        ResetToRoot(shell);

        shell.CanHandlePlatformBack().Should().BeFalse();
        shell.TryHandlePlatformBack().Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Modal
    // ─────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Back_closes_open_modal()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            shell.ShowModal(new TextBlock { Text = "modal" });

            shell.CanHandlePlatformBack().Should().BeTrue();
            shell.TryHandlePlatformBack().Should().BeTrue();
            shell.IsModalOpen.Should().BeFalse("back must close the modal");
            shell.TryHandlePlatformBack().Should().BeFalse("second back is at root again");
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    [AvaloniaFact]
    public void Modal_wins_over_open_project_detail()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            FindProjectsVm(shell).OpenProjectDetail(new ProjectItemViewModel { ProjectName = "P" });
            shell.ShowModal(new TextBlock { Text = "modal" });

            shell.TryHandlePlatformBack().Should().BeTrue();
            shell.IsModalOpen.Should().BeFalse("first back closes the modal, not the detail");
            FindProjectsVm(shell).SelectedProject.Should().NotBeNull("detail must survive the modal close");

            shell.TryHandlePlatformBack().Should().BeTrue();
            FindProjectsVm(shell).SelectedProject.Should().BeNull("second back closes the detail");
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Investor detail flow
    // ─────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Back_closes_project_detail()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            FindProjectsVm(shell).OpenProjectDetail(new ProjectItemViewModel { ProjectName = "P" });

            shell.CanHandlePlatformBack().Should().BeTrue();
            shell.TryHandlePlatformBack().Should().BeTrue();
            FindProjectsVm(shell).SelectedProject.Should().BeNull();
            shell.TryHandlePlatformBack().Should().BeFalse();
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    [AvaloniaFact]
    public void Back_closes_investment_detail()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            PortfolioVm().OpenInvestmentDetail(new InvestmentViewModel
            {
                ProjectName = "P",
                Stages = new ObservableCollection<InvestmentStageViewModel>(),
            });

            shell.CanHandlePlatformBack().Should().BeTrue();
            shell.TryHandlePlatformBack().Should().BeTrue();
            PortfolioVm().SelectedInvestment.Should().BeNull();
            shell.TryHandlePlatformBack().Should().BeFalse();
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Founder create-wizard flow
    // ─────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Back_steps_back_through_create_wizard_then_closes_it()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            var mp = MyProjectsVm(shell);
            mp.LaunchCreateWizard();
            mp.CreateProjectVm.MaxStepReached = 3; // GoToStep is clamped to MaxStepReached
            mp.CreateProjectVm.GoToStep(3);

            shell.CanHandlePlatformBack().Should().BeTrue();

            shell.TryHandlePlatformBack().Should().BeTrue();
            mp.CreateProjectVm.CurrentStep.Should().Be(2, "back must step the wizard back, not close it");
            mp.ShowCreateWizard.Should().BeTrue();

            shell.TryHandlePlatformBack().Should().BeTrue();
            mp.CreateProjectVm.CurrentStep.Should().Be(1);
            mp.ShowCreateWizard.Should().BeTrue();

            shell.TryHandlePlatformBack().Should().BeTrue();
            mp.ShowCreateWizard.Should().BeFalse("back at step 1 closes the wizard");

            shell.TryHandlePlatformBack().Should().BeFalse();
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Founder edit-profile flow
    // ─────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Back_closes_edit_profile()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            MyProjectsVm(shell).OpenEditProfile(new MyProjectItemViewModel
            {
                Name = "P",
                ProjectType = "fund",
                ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
            });

            shell.CanHandlePlatformBack().Should().BeTrue();
            shell.TryHandlePlatformBack().Should().BeTrue();
            MyProjectsVm(shell).SelectedEditProject.Should().BeNull();
            shell.TryHandlePlatformBack().Should().BeFalse();
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Founder manage-funds flow
    // ─────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Back_closes_manage_funds()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            MyProjectsVm(shell).OpenManageProject(new MyProjectItemViewModel
            {
                Name = "P",
                ProjectType = "fund",
                ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
            });

            shell.CanHandlePlatformBack().Should().BeTrue();
            shell.TryHandlePlatformBack().Should().BeTrue();
            MyProjectsVm(shell).SelectedManageProject.Should().BeNull();
            shell.TryHandlePlatformBack().Should().BeFalse();
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    /// <summary>
    /// Manage Project's claim/release/debug modals are inline overlays bound directly
    /// to ManageProjectViewModel flags (ManageProjectModalsView's own ZIndex=200 backdrop),
    /// not shell-level ShowModal() calls — so they need their own back-ladder entry.
    /// Before this fix, back would skip past the open modal straight to closing the
    /// entire Manage Project screen.
    /// </summary>
    [AvaloniaFact]
    public void Back_closes_manage_funds_inner_modal_before_manage_funds_itself()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            var mp = MyProjectsVm(shell);
            mp.OpenManageProject(new MyProjectItemViewModel
            {
                Name = "P",
                ProjectType = "fund",
                ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
            });
            mp.SelectedManageProject!.ShowReleaseFundsPasswordModal = true;

            shell.CanHandlePlatformBack().Should().BeTrue();

            shell.TryHandlePlatformBack().Should().BeTrue();
            mp.SelectedManageProject!.ShowReleaseFundsPasswordModal.Should().BeFalse("first back closes the inner modal, not Manage Project");
            mp.SelectedManageProject.Should().NotBeNull("Manage Project must survive the inner-modal close");

            shell.TryHandlePlatformBack().Should().BeTrue();
            mp.SelectedManageProject.Should().BeNull("second back closes Manage Project itself");

            shell.TryHandlePlatformBack().Should().BeFalse();
        }
        finally
        {
            ResetToRoot(shell);
        }
    }

    // ────────────────────────────────────────────────────────────
    // Settings inner modals
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Settings' network-switch and wipe-data confirmations are inline overlays bound
    /// directly to SettingsViewModel flags (SettingsView.axaml IsVisible bindings), not
    /// shell-level ShowModal() calls, and Settings itself is a root-level destination
    /// (like Home) that CanHandlePlatformBack correctly ignores — so without this entry
    /// back would fall through to Android's default (minimize/exit) while one of these
    /// modals was open.
    /// </summary>
    [AvaloniaFact]
    public void Back_closes_settings_inner_modal()
    {
        var shell = GetShell();
        ResetToRoot(shell);
        try
        {
            shell.IsSettingsOpen = true;
            SettingsVm(shell).OpenWipeDataModal();

            shell.CanHandlePlatformBack().Should().BeTrue();
            shell.TryHandlePlatformBack().Should().BeTrue();
            SettingsVm(shell).IsWipeDataModalOpen.Should().BeFalse();
            shell.TryHandlePlatformBack().Should().BeFalse("Settings itself is a root destination, like Home");
        }
        finally
        {
            ResetToRoot(shell);
        }
    }
}

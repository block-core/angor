using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Angor.Shared.Services;
using App.UI.Shell;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.Test.Integration;

/// <summary>
/// Regression tests for the shell-level "Indexer unreachable" popup (issue #971).
///
/// ShellViewModel.ShowModal is a single-slot host: showing a new modal silently
/// destroys whatever modal is currently open. The popup is raised from a background
/// event (INetworkService.IndexerUnreachable), so before this guard it could fire
/// mid-payment and rip an in-flight invest/deploy PaymentFlow modal out of the
/// visual tree — unrecoverable, and the cause of repeated UAT failures at invest
/// steps ("WalletButton not found") whenever the signet indexer returned a 5xx.
///
/// The guard: when any modal is already open, the popup is skipped (not queued)
/// and its "open" latch is reset, so NetworkService's cooldown re-raise can show
/// it later once the modal slot is free.
/// </summary>
public class IndexerUnreachablePopupTests
{
    private static ShellViewModel GetShell() =>
        global::App.App.Services.GetRequiredService<ShellViewModel>();

    private static INetworkService GetNetworkService() =>
        global::App.App.Services.GetRequiredService<INetworkService>();

    /// <summary>Dismiss the popup through its own Close path so the private
    /// _isIndexerUnreachableModalOpen latch inside ShellViewModel is reset.</summary>
    private static void ClosePopupProperly(ShellViewModel shell)
    {
        if (shell.ModalContent is IndexerUnreachableModal modal &&
            modal.DataContext is IndexerUnreachableModalViewModel vm)
        {
            vm.Close();
        }
        else
        {
            shell.HideModal();
        }
    }

    [AvaloniaFact]
    public void Popup_does_not_clobber_an_open_modal()
    {
        var shell = GetShell();
        shell.HideModal();

        var paymentModal = new Border(); // stand-in for an in-flight PaymentFlow modal
        shell.ShowModal(paymentModal);

        GetNetworkService().NotifyIndexerUnreachable(
            "https://unreachable-popup-test-1.invalid", "HTTP 500");
        Dispatcher.UIThread.RunJobs();

        shell.ModalContent.Should().BeSameAs(paymentModal,
            "the indexer-unreachable popup must never replace an open modal — " +
            "destroying an in-flight payment flow is unrecoverable");

        shell.HideModal();
    }

    [AvaloniaFact]
    public void Popup_shows_when_no_modal_is_open()
    {
        var shell = GetShell();
        shell.HideModal();

        GetNetworkService().NotifyIndexerUnreachable(
            "https://unreachable-popup-test-2.invalid", "HTTP 500");
        Dispatcher.UIThread.RunJobs();

        shell.ModalContent.Should().BeOfType<IndexerUnreachableModal>();

        ClosePopupProperly(shell);
        shell.ModalContent.Should().BeNull();
    }

    [AvaloniaFact]
    public void Skipped_popup_does_not_latch_and_can_show_later()
    {
        var shell = GetShell();
        shell.HideModal();

        // 1. Event fires while a modal is open — popup skipped.
        var paymentModal = new Border();
        shell.ShowModal(paymentModal);
        GetNetworkService().NotifyIndexerUnreachable(
            "https://unreachable-popup-test-3.invalid", "HTTP 500");
        Dispatcher.UIThread.RunJobs();
        shell.ModalContent.Should().BeSameAs(paymentModal);

        // 2. Modal closes, a later event (cooldown re-raise uses the same
        //    mechanism) must still be able to show the popup — i.e. the skip
        //    must not have left _isIndexerUnreachableModalOpen latched true.
        shell.HideModal();
        GetNetworkService().NotifyIndexerUnreachable(
            "https://unreachable-popup-test-4.invalid", "HTTP 500");
        Dispatcher.UIThread.RunJobs();

        shell.ModalContent.Should().BeOfType<IndexerUnreachableModal>(
            "a popup skipped while a modal was open must not permanently " +
            "suppress future indexer-unreachable popups");

        ClosePopupProperly(shell);
    }
}

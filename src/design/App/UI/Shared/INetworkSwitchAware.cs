namespace App.UI.Shared;

/// <summary>
/// Implemented by section ViewModels that hold network-derived state.
///
/// On mobile the shell keeps all section views (and their ViewModels) alive
/// across a network switch — the view cache is only cleared on desktop — so
/// every VM with network-derived state must reset itself explicitly.
/// <see cref="Shell.ShellViewModel.ResetAfterNetworkSwitch"/> iterates the
/// view cache and calls this on every DataContext that implements it, which
/// removes the need for a hand-maintained per-section reset list.
/// </summary>
public interface INetworkSwitchAware
{
    /// <summary>
    /// Clear all state derived from the previous network and, where
    /// appropriate, kick off a reload for the new network. Called on the UI
    /// thread immediately after the network configuration has been switched.
    /// </summary>
    void ResetAfterNetworkSwitch();
}

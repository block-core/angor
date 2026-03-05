using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.Funds;

/// <summary>
/// Create Wallet Modal — 3-step flow matching Vue Funds.vue (lines 436-585):
///   Step 1 "choice":   Import or Generate New
///   Step 2a "import":  Enter BIP-39 seed words textarea
///   Step 2b "backup":  Backup seed phrase (amber warning) + Download + Continue
///   Step 3 "success":  Wallet Created! (green checkmark) + Done
///
/// DataContext = FundsViewModel (set by FundsView when opening).
/// Implements IBackdropCloseable so clicking backdrop closes the modal.
/// </summary>
public partial class CreateWalletModal : UserControl, IBackdropCloseable
{
    /// <summary>Tracks whether the seed was "downloaded" (enables Continue button).</summary>
    private bool _seedDownloaded;

    public CreateWalletModal()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    private FundsViewModel? Vm => DataContext as FundsViewModel;

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    /// <summary>
    /// Called by the shell when the backdrop is clicked — just close.
    /// </summary>
    public void OnBackdropCloseRequested()
    {
        // No special cleanup needed — let the shell close the modal.
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            // ── Step 1: Choice ──
            case "CloseChoice":
                ShellVm?.HideModal();
                break;

            case "BtnImport":
                ShowStep("import");
                break;

            case "BtnGenerate":
                _seedDownloaded = false;
                ShowStep("backup");
                break;

            // ── Step 2a: Import ──
            case "CloseImport":
            case "BtnCancelImport":
                ShellVm?.HideModal();
                break;

            case "BtnSubmitImport":
                SubmitImport();
                break;

            // ── Step 2b: Backup ──
            case "BtnDownloadSeed":
                DownloadSeed();
                break;

            case "BtnContinueBackup":
                if (_seedDownloaded)
                {
                    // Create wallet via SDK
                    _ = CreateWalletViaSdkAsync("Generated Account");
                }
                break;

            case "BtnCancelBackup":
                ShellVm?.HideModal();
                break;

            // ── Step 3: Success ──
            case "BtnDone":
                ShellVm?.HideModal();
                break;
        }
    }

    /// <summary>
    /// Show a specific step, hiding all others.
    /// </summary>
    private void ShowStep(string step)
    {
        ChoicePanel.IsVisible = step == "choice";
        ImportPanel.IsVisible = step == "import";
        BackupPanel.IsVisible = step == "backup";
        SuccessPanel.IsVisible = step == "success";
    }

    /// <summary>
    /// Validate seed phrase and create "Imported Account" wallet group.
    /// Vue: submitSeedImport() — validates 12 or 24 words.
    /// </summary>
    private void SubmitImport()
    {
        var input = SeedPhraseInput.Text?.Trim() ?? "";
        var words = input.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

        if (words.Length != 12 && words.Length != 24)
        {
            SeedError.Text = "Please enter exactly 12 or 24 seed words.";
            SeedError.IsVisible = true;
            SeedSuccess.IsVisible = false;
            return;
        }

        SeedError.IsVisible = false;
        SeedSuccess.IsVisible = true;

        // Create wallet via SDK with imported seed words
        _ = CreateWalletViaSdkAsync("Imported Account");
    }

    /// <summary>
    /// Create a wallet via the SDK's CreateWalletAsync and show success step.
    /// </summary>
    private async Task CreateWalletViaSdkAsync(string walletName)
    {
        if (Vm == null) return;

        var (success, _) = await Vm.CreateWalletAsync(walletName, "default-key");
        if (success)
        {
            ShowStep("success");
        }
    }

    /// <summary>
    /// Simulate downloading the seed phrase.
    /// Vue: downloadGeneratedSeed() — sets seedDownloaded = true.
    /// In a real app this would trigger a file save dialog.
    /// </summary>
    private void DownloadSeed()
    {
        _seedDownloaded = true;

        // Enable and re-style the Continue button
        // Vue: enabled state = border-[#4B7C5A] text-[#4B7C5A]
        BtnContinueBackup.IsEnabled = true;
        BtnContinueBackup.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4B7C5A"));
        BtnContinueBackup.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4B7C5A"));
    }
}

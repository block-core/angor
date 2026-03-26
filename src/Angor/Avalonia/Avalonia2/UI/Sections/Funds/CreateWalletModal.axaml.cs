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

    private async void OnButtonClick(object? sender, RoutedEventArgs e)
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
                await SubmitImportAsync();
                break;

            // ── Step 2b: Backup ──
            case "BtnDownloadSeed":
                DownloadSeed();
                break;

            case "BtnContinueBackup":
                if (_seedDownloaded)
                    await GenerateWalletAsync();
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
    private async Task SubmitImportAsync()
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

        // Show spinner, disable button to prevent double-tap
        SetImportLoading(true);

        // Add the imported wallet group (will be async once SDK is wired)
        Vm?.AddWalletGroup("Imported Account", "import");
        await Task.Delay(600); // let the UI settle before transition

        SetImportLoading(false);
        ShowStep("success");
    }

    /// <summary>
    /// Generate wallet with loading state on Continue button.
    /// </summary>
    private async Task GenerateWalletAsync()
    {
        SetGenerateLoading(true);

        Vm?.AddWalletGroup("Generated Account", "generate");
        await Task.Delay(600);

        SetGenerateLoading(false);
        ShowStep("success");
    }

    private void SetImportLoading(bool loading)
    {
        BtnSubmitImport.IsEnabled = !loading;
        BtnCancelImport.IsEnabled = !loading;
        ImportBtnContent.IsVisible = !loading;
        ImportBtnSpinner.IsVisible = loading;
    }

    private void SetGenerateLoading(bool loading)
    {
        BtnContinueBackup.IsEnabled = !loading;
        BtnCancelBackup.IsEnabled = !loading;
        ContinueBtnContent.IsVisible = !loading;
        ContinueBtnSpinner.IsVisible = loading;
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

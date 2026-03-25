using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shell;

namespace App.UI.Sections.Funds;

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

    /// <summary>Generated seed words stored for wallet creation after backup confirmation.</summary>
    private string? _generatedSeedWords;

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
                GenerateAndDisplaySeed();
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
    /// Validate seed phrase and import wallet with user-provided seed words.
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

        // Import wallet with the user's seed words
        _ = ImportWalletViaSdkAsync("Imported Account", string.Join(" ", words));
    }

    /// <summary>
    /// Import a wallet via the SDK with user-provided seed words.
    /// </summary>
    private async Task ImportWalletViaSdkAsync(string walletName, string seedWords)
    {
        if (Vm == null) return;

        var success = await Vm.ImportWalletAsync(walletName, seedWords, "default-key");
        if (success)
        {
            ShowStep("success");
        }
    }

    /// <summary>
    /// Generate real seed words from the SDK and display them in the backup panel.
    /// </summary>
    private void GenerateAndDisplaySeed()
    {
        if (Vm == null) return;
        _generatedSeedWords = Vm.GenerateSeedWords();
        if (!string.IsNullOrEmpty(_generatedSeedWords))
        {
            SeedPhraseDisplay.Text = _generatedSeedWords;
        }
    }

    /// <summary>
    /// Create a new wallet via the SDK (generate flow) using pre-generated seed words.
    /// </summary>
    private async Task CreateWalletViaSdkAsync(string walletName)
    {
        if (Vm == null || string.IsNullOrEmpty(_generatedSeedWords)) return;

        var success = await Vm.ImportWalletAsync(walletName, _generatedSeedWords, "default-key");
        if (success)
        {
            ShowStep("success");
        }
    }

    /// <summary>
    /// Save seed phrase to a text file via file save dialog.
    /// Vue: downloadGeneratedSeed() — sets seedDownloaded = true.
    /// </summary>
    private async void DownloadSeed()
    {
        if (string.IsNullOrEmpty(_generatedSeedWords)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Save Seed Phrase",
                    SuggestedFileName = "seed-backup.txt",
                    DefaultExtension = "txt"
                });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new System.IO.StreamWriter(stream);
                await writer.WriteAsync(_generatedSeedWords);
            }
        }

        _seedDownloaded = true;
        BtnContinueBackup.IsEnabled = true;
        BtnContinueBackup.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4B7C5A"));
        BtnContinueBackup.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4B7C5A"));
    }
}

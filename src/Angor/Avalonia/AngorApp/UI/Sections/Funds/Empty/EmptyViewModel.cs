using AngorApp.UI.Sections.Wallet.CreateAndImport;
using Zafiro.Avalonia.Dialogs;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Sections.Funds.Empty
{
    public class EmptyViewModel(WalletCreationWizard walletCreationWizard, WalletImportWizard walletImportWizard, UIServices uiServices) : IEmptyViewModel
    {
        public IEnhancedCommand<Unit> AddWallet { get;  } = EnhancedCommand.Create(() => uiServices.Dialog.Show("", "Add New Wallet", closeable => 
        [
            new Option("Generate New", EnhancedCommand.CreateWithResult(() =>
            {
                closeable.Close();
                return walletCreationWizard.Start();
            }), new Zafiro.Avalonia.Dialogs.Settings()),
            new Option("Import", EnhancedCommand.CreateWithResult(() =>
            {
                closeable.Close();
                return walletImportWizard.Start();
            }), new Zafiro.Avalonia.Dialogs.Settings())
        ]));
    }
}
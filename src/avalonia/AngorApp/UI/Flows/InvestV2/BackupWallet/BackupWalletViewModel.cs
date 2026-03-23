using Zafiro.Avalonia.Dialogs;
using Option = Zafiro.Avalonia.Dialogs.Option;

using AngorApp.UI.Flows.InvestV2.InvestmentResult;
using AngorApp.UI.Shell;

namespace AngorApp.UI.Flows.InvestV2.BackupWallet;

public class BackupWalletViewModel(UIServices uiServices) : IBackupWalletViewModel
{
    public IEnumerable<IOption> Options(ICloseable closeable, IShellViewModel shell)
    {
        return
        [
            new Option("Download Seed", EnhancedCommand.Create(() => DownloadSeed()), new Settings() { Role = OptionRole.Info }),
            new Option("Continue", EnhancedCommand.Create(() =>
            {
                closeable.Close();
                return uiServices.Dialog.Show(
                    new InvestResultViewModel(shell),
                    "Investment Completed",
                    (model, closeable) => model.Options(closeable));
            }), new Settings() { Role = OptionRole.Primary, IsDefault = true})
        ];
    }

    private Result<object> DownloadSeed()
    {
        // TODO: Implement the seed download logic
        return Result.Success();
    }
}
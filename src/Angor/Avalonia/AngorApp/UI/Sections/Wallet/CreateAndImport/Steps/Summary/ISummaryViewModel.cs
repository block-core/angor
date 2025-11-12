
namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.Summary;

public interface ISummaryViewModel
{
    public bool IsRecovery { get; }
    public string TitleText { get; }
    string CreateWalletText { get; }
    string CreatingWalletText { get; }
    ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
    Maybe<string> Passphrase { get; }
}
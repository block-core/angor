using Angor.UI.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SummaryAndCreation;

public interface ISummaryAndCreationViewModel
{
    public Maybe<string> Passphrase { get; }
    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
    string CreateWalletText { get; }
    string CreatingWalletText { get;  }
    public bool IsRecovery { get; }
    public string TitleText { get; }
}
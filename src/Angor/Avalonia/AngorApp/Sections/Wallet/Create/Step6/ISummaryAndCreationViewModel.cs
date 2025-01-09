using AngorApp.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.Create.Step6;

public interface ISummaryAndCreationViewModel
{
    public Maybe<string> Passphrase { get; }
    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
}
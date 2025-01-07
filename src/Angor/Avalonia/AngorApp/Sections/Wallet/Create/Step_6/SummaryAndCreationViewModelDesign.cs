using AngorApp.Model;
using AngorApp.Sections.Browse;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.Create.Step_6;

public class SummaryAndCreationViewModelDesign : ISummaryAndCreationViewModel
{
    public WordList Seedwords { get; } = SampleData.Seedwords;
    public Maybe<string> Passphrase { get; } = Maybe<string>.None;
    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
}
using AngorApp.Model;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Wallet.Create.Step_2;
using AngorApp.Sections.Wallet.Create.Step_6;
using CSharpFunctionalExtensions;

namespace AngorApp;

public class SummaryAndCreationViewModelDesign : ISummaryAndCreationViewModel
{
    public WordList Seedwords { get; } = SampleData.Seedwords;
    public Maybe<string> Passphrase { get; } = Maybe<string>.None;
    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
}
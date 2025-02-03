using AngorApp.Model;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsGeneration;

public interface ISeedWordsViewModel : IStep
{
    Maybe<SeedWords> Words { get; }
    ReactiveCommand<Unit, Maybe<SeedWords>> GenerateWords { get; }
    bool AreWordsWrittenDown { get; set; }
}
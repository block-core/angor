using AngorApp.Core;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsGeneration;

public interface ISeedWordsViewModel
{
    SafeMaybe<SeedWords> Words { get; }
    ReactiveCommand<Unit, SafeMaybe<SeedWords>> GenerateWords { get; }
    bool AreWordsWrittenDown { get; set; }
}
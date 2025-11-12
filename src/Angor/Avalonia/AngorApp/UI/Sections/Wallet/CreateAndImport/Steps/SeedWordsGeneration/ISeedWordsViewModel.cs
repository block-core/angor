using AngorApp.Core;

namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.SeedWordsGeneration;

public interface ISeedWordsViewModel
{
    SafeMaybe<SeedWords> Words { get; }
    ReactiveCommand<Unit, SafeMaybe<SeedWords>> GenerateWords { get; }
    bool AreWordsWrittenDown { get; set; }
}
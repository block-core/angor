using Angor.UI.Model;
using AngorApp.Core;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsGeneration;

public interface ISeedWordsViewModel : IStep
{
    SafeMaybe<SeedWords> Words { get; }
    ReactiveCommand<Unit, SafeMaybe<SeedWords>> GenerateWords { get; }
    bool AreWordsWrittenDown { get; set; }
}
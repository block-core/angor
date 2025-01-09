using AngorApp.Model;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Create.Step2;

public interface ISeedWordsViewModel : IStep
{
    Maybe<WordList> Words { get; }
    ReactiveCommand<Unit, Maybe<WordList>> GenerateWords { get; }
    bool AreWordsWrittenDown { get; set; }
}
using AngorApp.Model;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoverySeedWords;

public interface IRecoverySeedWordsViewModel : IStep
{
    string? RawWordList { get; set; }
    SeedWords SeedWords { get; }
}
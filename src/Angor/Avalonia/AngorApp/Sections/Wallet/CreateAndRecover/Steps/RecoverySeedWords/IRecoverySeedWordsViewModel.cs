namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoverySeedWords;

public interface IRecoverySeedWordsViewModel
{
    string? RawWordList { get; set; }
    SeedWords SeedWords { get; }
}
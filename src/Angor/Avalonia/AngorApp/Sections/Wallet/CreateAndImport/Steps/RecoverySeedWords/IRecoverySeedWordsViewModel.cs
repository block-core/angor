namespace AngorApp.Sections.Wallet.CreateAndImport.Steps.RecoverySeedWords;

public interface IRecoverySeedWordsViewModel
{
    string? RawWordList { get; set; }
    SeedWords SeedWords { get; }
}
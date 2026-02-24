namespace AngorApp.UI.Sections.Wallet;

public record WalletImportOptions(SeedWords Seedwords, Maybe<string> Passphrase);
namespace AngorApp.Sections.Wallet;

public record WalletImportOptions(SeedWords Seedwords, Maybe<string> Passphrase, string EncryptionKey);
namespace Angor.Client.Storage;

public static class FounderKeyAccess
{
    public const string InvestorOnlyMessage =
        "This browser wallet is investor-only. Import your recovery phrase into the Angor app to create or manage projects.";

    /// <summary>
    /// True when the stored wallet has founder keys. Browser wallets created by the invest flow have none.
    /// </summary>
    public static bool HasFounderKeys(IWalletStorage walletStorage)
    {
        return walletStorage.HasWallet() && walletStorage.GetFounderKeys()?.Keys is { Count: > 0 };
    }
}

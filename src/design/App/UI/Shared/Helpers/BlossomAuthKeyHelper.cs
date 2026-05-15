using Blockcore.NBitcoin;

namespace App.UI.Shared.Helpers;

public static class BlossomAuthKeyHelper
{
    public static string CreateEphemeralPrivateKeyHex() =>
        Convert.ToHexString(new Key().ToBytes()).ToLowerInvariant();
}

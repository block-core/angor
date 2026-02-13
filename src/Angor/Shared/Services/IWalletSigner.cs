using Blockcore.NBitcoin;

namespace Angor.Shared.Services;

public interface IWalletSigner
{
    Key GetPrivateKey(int accountIndex, int addressIndex, bool isChange);
    PubKey GetPublicKey(int accountIndex, int addressIndex, bool isChange);
    ExtKey GetAccountExtKey(int accountIndex);
    Key GetPrivateKey(string hdPath);
    string GetRootExtPubKey();
}

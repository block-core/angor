using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared;

public interface IDerivationOperations
{
    FounderKeyCollection DeriveProjectKeys(WalletWords walletWords, string angorTestKey);
    FounderKeys GetProjectKey(FounderKeyCollection founderKeyCollection, int index);
    string DeriveFounderKey(WalletWords walletWords, int index);
    string DeriveFounderRecoveryKey(WalletWords walletWords, int index);
    uint DeriveProjectId(string founderKey);
    string DeriveAngorKey(string founderKey, string angorRootKey);
    Script AngorKeyToScript(string angorKey);
    string DeriveInvestorKey(WalletWords walletWords, string founderKey);
    Key DeriveInvestorPrivateKey(WalletWords walletWords, string founderKey);
    string DeriveSeederSecretHash(WalletWords walletWords, string founderKey);
    Key DeriveFounderPrivateKey(WalletWords walletWords, int index);

    Key DeriveFounderRecoveryPrivateKey(WalletWords walletWords, int index);
    Key DeriveProjectNostrInvestorPrivateKey(WalletWords walletWords, string projectId);
    Key DeriveProjectNostrPrivateKey(WalletWords walletWords, int index);
    Task<Key> DeriveProjectNostrPrivateKeyAsync(WalletWords walletWords, int index);
}
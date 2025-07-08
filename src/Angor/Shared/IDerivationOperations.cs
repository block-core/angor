using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared;

public interface IDerivationOperations
{
    FounderKeyCollection DeriveProjectKeys(WalletWords walletWords, string angorTestKey);
    FounderKeys GetProjectKey(FounderKeyCollection founderKeyCollection, int index);
    string DeriveFounderKey(WalletWords walletWords, int index);
    string DeriveFounderRecoveryKey(WalletWords walletWords, string founderKey);
    uint DeriveUniqueProjectIdentifier(string founderKey);
    string DeriveAngorKey(string angorRootKey, string founderKey);
    Script AngorKeyToScript(string angorKey);
    string DeriveInvestorKey(WalletWords walletWords, string founderKey);
    Key DeriveInvestorPrivateKey(WalletWords walletWords, string founderKey);
    string DeriveLeadInvestorSecretHash(WalletWords walletWords, string founderKey);
    Key DeriveFounderPrivateKey(WalletWords walletWords, int index);
    Key DeriveFounderRecoveryPrivateKey(WalletWords walletWords, string founderKey);
    Key DeriveProjectNostrPrivateKey(WalletWords walletWords, string founderKey);
    string DeriveNostrPubKey(WalletWords walletWords, string founderKey);
    Task<Key> DeriveProjectNostrPrivateKeyAsync(WalletWords walletWords, string founderKey);
    string DeriveNostrStoragePassword(WalletWords walletWords);
    Key DeriveNostrStorageKey(WalletWords walletWords);
    string DeriveNostrStoragePubKeyHex(WalletWords walletWords);
}
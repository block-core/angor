using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;

namespace Angor.Shared;

public interface IDerivationOperations
{
    FounderKeyCollection DeriveProjectKeys(WalletWords walletWords, string angorTestKey);
    FounderKeys GetProjectKey(FounderKeyCollection founderKeyCollection, int index);
    string DeriveFounderKey(WalletWords walletWords, int index);
    string DeriveFounderRecoveryKey(WalletWords walletWords, string founderKey);
    uint DeriveUniqueProjectIdentifier(string founderKey);
    (uint Hi, uint Lo) DeriveProjectIndicesV2(string founderKey);
    string DeriveAngorKey(string angorRootKey, string founderKey);
    Script AngorKeyToScript(string angorKey);
    string DeriveInvestorKey(WalletWords walletWords, string founderKey);
    AngorKey DeriveInvestorPrivateKey(WalletWords walletWords, string founderKey);
    string DeriveLeadInvestorSecretHash(WalletWords walletWords, string founderKey);
    AngorKey DeriveFounderPrivateKey(WalletWords walletWords, int index);
    AngorKey DeriveFounderRecoveryPrivateKey(WalletWords walletWords, string founderKey);
    AngorKey DeriveProjectNostrPrivateKey(WalletWords walletWords, string founderKey);
    string DeriveNostrPubKey(WalletWords walletWords, string founderKey);
    Task<AngorKey> DeriveProjectNostrPrivateKeyAsync(WalletWords walletWords, string founderKey);
    string DeriveNostrStoragePassword(WalletWords walletWords);
    AngorKey DeriveNostrStorageKey(WalletWords walletWords);
    string DeriveNostrStoragePubKeyHex(WalletWords walletWords);
    string ConvertAngorKeyToBitcoinAddress(string projectId);
    AngorKey DeriveSupportDmKey(WalletWords walletWords);
    string DeriveSupportDmPubKeyHex(WalletWords walletWords);
}
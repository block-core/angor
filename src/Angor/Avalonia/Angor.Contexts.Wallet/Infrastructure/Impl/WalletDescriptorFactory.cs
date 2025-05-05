using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;
using NBitcoin;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public abstract class WalletDescriptorFactory
{
    public static WalletDescriptor Create(string seedPhrase, Maybe<string> passphrase, Network network)
    {
        if (string.IsNullOrWhiteSpace(seedPhrase))
            throw new DomainException("Invalid seed phrase");

        var masterKey = new Mnemonic(seedPhrase).DeriveExtKey(passphrase.GetValueOrDefault());

        // Determine the coin_type based on the network (0 for MainNet, 1 for TestNet)
        uint coinType = (uint)(network == Network.TestNet ? 1 : 0);

        // Derive xpub for Segwit (P2WPKH)
        var segwitPath = $"m/84'/{coinType}'/0'";
        var segwitXPub = XPub.Create(
            masterKey.Derive(new KeyPath(segwitPath)).Neuter().ToString(network),
            DomainScriptType.SegWit,
            DerivationPath.Create(84, coinType, 0)
        );

        // Derive xpub for Taproot (P2TR)
        var taprootPath = $"m/86'/{coinType}'/0'";
        var taprootXPub = XPub.Create(
            masterKey.Derive(new KeyPath(taprootPath)).Neuter().ToString(network),
            DomainScriptType.Taproot,
            DerivationPath.Create(86, coinType, 0)
        );

        var xpubs = new XPubCollection([segwitXPub, taprootXPub]);

        return new WalletDescriptor(network.FromNBitcoin(), new XPubCollection(xpubs));
    }
}
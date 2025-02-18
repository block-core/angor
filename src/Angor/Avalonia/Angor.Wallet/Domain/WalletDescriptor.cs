namespace Angor.Wallet.Domain;

public sealed record WalletDescriptor(BitcoinNetwork Network, XPubCollection Xpubs);
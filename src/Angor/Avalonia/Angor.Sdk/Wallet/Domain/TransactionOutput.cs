using Angor.Sdk.Common;

namespace Angor.Sdk.Wallet.Domain;

public record TransactionOutput(Amount Amount, Address Address);
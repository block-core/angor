using Angor.Sdk.Common;

namespace Angor.Sdk.Wallet.Domain;

public record TransactionInput(Amount Amount, Address Address);
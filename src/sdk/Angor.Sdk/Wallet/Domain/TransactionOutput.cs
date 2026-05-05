using Angor.Sdk.Common;

using Angor.Primitives;

namespace Angor.Sdk.Wallet.Domain;

public record TransactionOutput(Amount Amount, Address Address);
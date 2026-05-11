using Angor.Sdk.Common;

using Angor.Primitives;

namespace Angor.Sdk.Wallet.Domain;

public record TransactionInput(Amount Amount, Address Address);
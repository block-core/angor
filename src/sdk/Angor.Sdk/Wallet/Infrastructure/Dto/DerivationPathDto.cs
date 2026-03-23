namespace Angor.Sdk.Wallet.Infrastructure.Dto;

public record DerivationPathDto(
    uint Purpose,
    uint CoinType,
    uint Account
);
namespace Angor.Contexts.Wallet.Infrastructure.Dto;

public record DerivationPathDto(
    uint Purpose,
    uint CoinType,
    uint Account
);
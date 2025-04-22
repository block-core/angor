namespace Angor.Contexts.Wallet.Infrastructure.Dto;

public record WalletDescriptorDto(
    string Network,
    IEnumerable<XPubDto> XPubs
);
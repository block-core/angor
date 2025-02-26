namespace Angor.Wallet.Infrastructure.Dto;

public record WalletDescriptorDto(
    string Network,
    IEnumerable<XPubDto> XPubs
);
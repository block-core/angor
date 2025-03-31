using Angor.Wallet.Domain;

namespace Angor.Wallet.Infrastructure.Dto;

public record XPubDto(
    string Value,
    DomainScriptType ScriptType,
    DerivationPathDto Path
);
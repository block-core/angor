using Angor.Contexts.Wallet.Domain;

namespace Angor.Contexts.Wallet.Infrastructure.Dto;

public record XPubDto(
    string Value,
    DomainScriptType ScriptType,
    DerivationPathDto Path
);
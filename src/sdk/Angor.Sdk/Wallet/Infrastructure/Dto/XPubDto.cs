using Angor.Sdk.Wallet.Domain;

namespace Angor.Sdk.Wallet.Infrastructure.Dto;

public record XPubDto(
    string Value,
    DomainScriptType ScriptType,
    DerivationPathDto Path
);
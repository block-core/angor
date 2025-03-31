namespace Angor.Wallet.Domain;

public enum DomainScriptType
{
    Invalid,
    SegWit,
    Taproot,
    Legacy,
    P2SH,
    P2WSH
}
namespace Angor.Contexts.Wallet.Domain;

public sealed record XPub
{
    public string Value { get; }
    public DomainScriptType ScriptType { get; }
    public DerivationPath Path { get; }

    private XPub(string value, DomainScriptType scriptType, DerivationPath path)
    {
        Value = value;
        ScriptType = scriptType;
        Path = path;
    }

    public static XPub Create(string value, DomainScriptType scriptType, DerivationPath path)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("XPub value cannot be empty");

        return new XPub(value, scriptType, path);
    }
}
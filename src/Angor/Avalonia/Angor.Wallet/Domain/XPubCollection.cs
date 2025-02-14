using System.Collections;

namespace Angor.Wallet.Domain;

public sealed record XPubCollection : IEnumerable<XPub>
{
    private readonly Dictionary<DomainScriptType, XPub> xpubs;

    private XPubCollection(Dictionary<DomainScriptType, XPub> xpubs)
    {
        this.xpubs = xpubs;
    }

    public static XPubCollection Create(XPub segwitXPub, XPub taprootXPub)
    {
        return new XPubCollection(new Dictionary<DomainScriptType, XPub>
        {
            { DomainScriptType.SegWit, segwitXPub },
            { DomainScriptType.Taproot, taprootXPub }
        });
    }

    public IEnumerator<XPub> GetEnumerator() => xpubs.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
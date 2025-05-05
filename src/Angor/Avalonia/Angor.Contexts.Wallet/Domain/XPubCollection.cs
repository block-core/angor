using System.Collections.ObjectModel;

namespace Angor.Contexts.Wallet.Domain;

public class XPubCollection(IEnumerable<XPub> xpubs) : Collection<XPub>(xpubs.ToList());
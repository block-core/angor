using System.Collections.ObjectModel;

namespace Angor.Wallet.Domain;

public class XPubCollection(IEnumerable<XPub> xpubs) : Collection<XPub>(xpubs.ToList());
using System.Collections.ObjectModel;

namespace Angor.Sdk.Wallet.Domain;

public class XPubCollection(IEnumerable<XPub> xpubs) : Collection<XPub>(xpubs.ToList());
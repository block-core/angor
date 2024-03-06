using Angor.Shared.Models;

namespace Angor.Shared.Utilities;

public static class LinqExtension 
{
    public static bool Remove(this List<Outpoint> list, Outpoint item)
    {
        var found = list.FirstOrDefault(_ => _.ToString() == item.ToString());

        return found != null && list.Remove(found);
    }

    public static bool Contains(this List<Outpoint> list, Outpoint item)
    {
        return list.Any(_ => _.ToString() == item.ToString());
    }
}
using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Shared.Models;

namespace Angor.Shared.Utilities;

public static class LinqExtension 
{
    public static bool TryRemoveOutpoint(this List<Outpoint> list, Outpoint item)
    {
        var found = list.FirstOrDefault(_ => _.ToString() == item.ToString());

        return found != null && list.Remove(found);
    }

    public static bool ContainsOutpoint(this List<Outpoint> list, Outpoint item)
    {
        return list.Any(_ => _.ToString() == item.ToString());
    }
}
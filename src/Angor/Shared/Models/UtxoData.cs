namespace Angor.Shared.Models;

public class UtxoData
{
    public Outpoint Outpoint { get; set; }
    public string Address { get; set; }
    public string ScriptHex { get; set; }
    public long Value { get; set; }
    public int BlockIndex { get; set; }
    public bool PendingSpent { get; set; }
}

public class UtxoDataWithPath
{
    public UtxoData UtxoData { get; set; }
    public string HdPath { get; set; }
}
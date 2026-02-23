namespace Angor.Shared.Models;

public class UtxoData
{
    public Outpoint outpoint { get; set; } = null!;
    public string address { get; set; } = string.Empty;
    public string scriptHex { get; set; } = string.Empty;
    public long value { get; set; }
    public int blockIndex { get; set; }
    public bool PendingSpent { get; set; }
}

public class UtxoDataWithPath
{
    public UtxoData UtxoData { get; set; } = null!;
    public string HdPath { get; set; } = string.Empty;
}
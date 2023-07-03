namespace Angor.Shared.Models;

public class UtxoData
{
    public Outpoint outpoint { get; set; }
    public string address { get; set; }
    public string scriptHex { get; set; }
    public long value { get; set; }
    public int blockIndex { get; set; }
    public bool coinBase { get; set; }
    public bool coinStake { get; set; }
}

public class UtxoDataWithPath
{
    public UtxoData UtxoData { get; set; }
    public string HdPath { get; set; }
}
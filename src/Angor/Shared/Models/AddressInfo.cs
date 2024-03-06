namespace Angor.Shared.Models;

public class AddressInfo
{
    public string Address { get; set; }
    public string HdPath { get; set; }
    public List<UtxoData> UtxoData { get; set; } = new();
    public bool HasHistory { get; set; }
    public long Balance => UtxoData.Sum(_ => _.Value);
}
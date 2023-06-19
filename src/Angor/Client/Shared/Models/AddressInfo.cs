namespace Angor.Client.Shared.Models;

public class AddressInfo
{
    public string HdPath { get; set; }
    public List<UtxoData> UtxoData { get; set; }
    public bool HasHistory { get; set; }
    public long Balance => UtxoData.Sum(_ => _.value);
}
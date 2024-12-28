namespace AngorApp.Model;

public class Destination
{
    public Destination(string name, ulong amount, string bitcoinAddress)
    {
        Name = name;
        Amount = amount;
        BitcoinAddress = bitcoinAddress;
    }

    public string Name { get; }
    public ulong Amount { get; }
    public string BitcoinAddress { get; }
}
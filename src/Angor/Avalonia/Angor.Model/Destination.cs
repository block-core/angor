namespace AngorApp.Model;

public class Destination
{
    public Destination(string name, long amount, string bitcoinAddress)
    {
        Name = name;
        Amount = amount;
        BitcoinAddress = bitcoinAddress;
    }

    public string Name { get; }
    public long Amount { get; }
    public string BitcoinAddress { get; }
}
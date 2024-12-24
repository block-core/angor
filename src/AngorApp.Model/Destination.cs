namespace AngorApp.Model;

public class Destination
{
    public Destination(string name, decimal amount, string bitcoinAddress)
    {
        Name = name;
        Amount = amount;
        BitcoinAddress = bitcoinAddress;
    }

    public string Name { get; }
    public decimal Amount { get; }
    public string BitcoinAddress { get; }
}
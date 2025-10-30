namespace AngorApp.Model.Domain.Amounts;

public class SendAmount
{
    public SendAmount(string name, long amount, string bitcoinAddress)
    {
        Name = name;
        Amount = amount;
        BitcoinAddress = bitcoinAddress;
    }

    public string Name { get; }
    public long Amount { get; }
    public string BitcoinAddress { get; }
}
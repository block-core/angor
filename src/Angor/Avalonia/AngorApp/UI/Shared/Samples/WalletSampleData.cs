using System.Linq;

namespace AngorApp.UI.Shared.Samples;

public static class WalletSampleData
{
    public static string TestNetBitcoinAddress { get; } = "mzHrLAR3WWLE4eCpq82BDCKmLeYRyYXPtm";

    public static SeedWords Seedwords
    {
        get
        {
            List<string> list = ["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"];
            return new SeedWords(list.Select((s, i) => new SeedWord(i + 1, s)));
        }
    }
}

namespace AngorApp.Model.Domain.Wallet;

public class SeedWord
{
    public SeedWord(int index, string text)
    {
        Index = index;
        Text = text;
    }

    public int Index { get; set; }
    public string Text { get; set; }

    public override string ToString()
    {
        return Text;
    }
}
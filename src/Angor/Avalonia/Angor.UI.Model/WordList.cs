using System.Collections.ObjectModel;
using Zafiro.Mixins;

namespace Angor.UI.Model;


public class SeedWords : Collection<SeedWord>
{
    public SeedWords()
    {
    }
    
    public SeedWords(IEnumerable<SeedWord> wordList) : base(wordList.ToList())
    {
    }

    public SeedWords(string rawWordList) : this(rawWordList.Split(" ").Select((s, i) => new SeedWord(i + 1, s)).ToList())
    {
    }

    public override string ToString()
    {
        return Items.Join(" ");
    }
}
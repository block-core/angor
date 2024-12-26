using System.Collections.ObjectModel;
using Zafiro.Mixins;

namespace AngorApp.Model;


public class WordList : Collection<SeedWord>
{
    public WordList()
    {
    }
    
    public WordList(IEnumerable<SeedWord> wordList) : base(wordList.ToList())
    {
    }

    public override string ToString()
    {
        return Items.JoinWithCommas();
    }
}
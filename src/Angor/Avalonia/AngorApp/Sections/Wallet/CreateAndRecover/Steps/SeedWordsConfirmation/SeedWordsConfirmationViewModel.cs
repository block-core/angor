using System.Linq;
// ReSharper disable once RedundantUsingDirective
using ReactiveUI.Validation.Extensions;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsConfirmation;

public class SeedWordsConfirmationViewModel : ISeedWordsConfirmationViewModel
{
    public SeedWordsConfirmationViewModel(SeedWords seedWords)
    {
        SeedWords = seedWords;
        Challenges = GetRandomIndices(seedWords.Count, 2).Select(i => seedWords[i]).Select(word => new Challenge(word)).ToList();
    }

    private static int[] GetRandomIndices(int totalWords, int count)
    {
        return Enumerable.Range(0, totalWords)
            .OrderBy(_ => Random.Shared.NextDouble())
            .Take(count)
            .ToArray();
    }
    
    #if !DEBUG
        public IObservable<bool> IsValid =>  Challenges.Select(x => x.IsValid()).CombineLatest(results => results.All(x => x));
    #else
        public IObservable<bool> IsValid => Observable.Return(true);
    #endif
    
    public SeedWords SeedWords { get; }
    public IEnumerable<Challenge> Challenges { get; }
}
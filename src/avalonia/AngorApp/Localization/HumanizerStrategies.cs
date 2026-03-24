using System;
using System.Globalization;
using Humanizer.DateTimeHumanizeStrategy;

namespace AngorApp.Localization;

internal sealed class InPrepositionDateTimeHumanizeStrategy : IDateTimeHumanizeStrategy
{
    private readonly IDateTimeHumanizeStrategy inner = new DefaultDateTimeHumanizeStrategy();

    public string Humanize(DateTime input, DateTime comparisonBase, CultureInfo culture)
    {
        var s = inner.Humanize(input, comparisonBase, culture);
        
        const string fromNow = " from now";
        if (s.EndsWith(fromNow, StringComparison.Ordinal))
        {
            var core = s[..^fromNow.Length];
            return $"in {core}";
        }

        return s;
    }
}

namespace AngorApp.UI.Shared;

public static class AmountRatioExtensions
{
    public static decimal RatioOrZero(this IAmountUI? numerator, IAmountUI? denominator)
    {
        if (numerator is null || denominator is null || denominator.Sats == 0)
        {
            return 0m;
        }

        return numerator.Sats / (decimal)denominator.Sats;
    }

    public static double RatioOrZeroAsDouble(this IAmountUI? numerator, IAmountUI? denominator)
    {
        if (numerator is null || denominator is null || denominator.Sats == 0)
        {
            return 0d;
        }

        return numerator.Sats / (double)denominator.Sats;
    }
}

using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Angor.Shared.Utilities;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Logging.Abstractions;

namespace AngorApp.UI.Shared.Controls
{
    public static class AngorConverters
    {
        private static readonly NostrConversionHelper NostrConversionHelper =
            new(new NullLogger<NostrConversionHelper>());

        public static readonly FuncValueConverter<DateTimeOffset, string> TimeLeftDateTimeOffset =
            new(offset => { return offset.Humanize(DateTimeOffset.Now); });

        public static readonly FuncValueConverter<DateTime, string> TimeLeftDateTime =
            new(offset => { return offset.Humanize(dateToCompareAgainst: DateTime.Now); });

        public static readonly FuncValueConverter<TimeSpan, string> HumanizeTimeSpan = new(ts => ts.Humanize(2));

        public static readonly FuncValueConverter<DateTimeOffset, string> HumanizeDateTimeOffset = new(offset =>
        {
            if (DateTimeOffset.Now.Date - offset < 2.Days())
            {
                return offset.Humanize();
            }

            return offset.ToString("d");
        });

        public static readonly FuncValueConverter<DateTime?, string?> HumanizeDateTime = new(offset =>
        {
            if (offset == null)
            {
                return null;
            }

            if (DateTime.Now.Date - offset < 2.Days())
            {
                return offset.Humanize();
            }

            return offset.Value.ToString("d");
        });

        public static readonly FuncValueConverter<int, string> MonthLabel = new(value =>
        {
            string? label = value == 1 ? "month" : "month".Pluralize();
            return label.Humanize(LetterCasing.Title);
        });


        public static readonly FuncValueConverter<object, string> ToJson =
            new(obt => JsonSerializer.Serialize(obt, new JsonSerializerOptions { WriteIndented = true }));


        public static readonly FuncMultiValueConverter<object?, string?> TimeStringFromPreviousStage =
            new(objects =>
            {
                List<object?> obs = objects.ToList();

                if (obs.Count < 2 ||
                    obs[0] is not int index ||
                    obs[1] is not TimeSpan time)
                {
                    return null;
                }

                if (time <= TimeSpan.Zero)
                {
                    return null;
                }

                string suffix = index == 1 ? "funding ends" : $"stage {index - 1}";

                return
                    $"{time.Humanize(minUnit: TimeUnit.Day, maxUnit: TimeUnit.Month, precision: 2, collectionSeparator: " and ")} after {suffix}";
            });

        public static FuncValueConverter<string, SvgImage> StringToQRCode { get; } = new(s =>
        {
            Debug.Assert(s != null, nameof(s) + " != null");
            return QRGenerator.SvgImageFrom(s);
        });

        public static IValueConverter HexToNpub { get; } =
            new FuncValueConverter<string?, string?>(s => s == null ? null : NostrConversionHelper.ConvertHexToNpub(s));

        public static IValueConverter FallbackIfNullOrEmpty { get; } = new FallbackIfNullOrEmptyConverter();
        public static IValueConverter SatsToBtc { get; } = new SatsToBtcConverter();

        public static IMultiValueConverter PercentOfTotalToBtcString { get; } =
            new FuncMultiValueConverter<object?, string?>(values =>
            {
                List<object?> obs = values.ToList();

                if (obs.Count < 2 ||
                    obs[0] == AvaloniaProperty.UnsetValue || obs[0] is null ||
                    obs[1] == AvaloniaProperty.UnsetValue || obs[1] is null)
                {
                    return null;
                }

                try
                {
                    decimal percent = System.Convert.ToDecimal(obs[0]);
                    long totalSats = System.Convert.ToInt64(obs[1]);

                    long resultSats = (long)(percent * totalSats);

                    IAmountUI amountUI = new AmountUI(resultSats);
                    return $" ({amountUI.BtcString})";
                }
                catch
                {
                    return null;
                }
            });

        public static IValueConverter OrdinalDateFormat { get; } =
            new FuncValueConverter<DateTime?, string?>(date =>
                date?.ToString("D", CultureInfo.CurrentCulture));

        public static IValueConverter AddMonthsToToday { get; } = new AddTodayToMonthsConverter();
        public static IValueConverter RatioToPercentage { get; } = new RatioToPercentageConverter();
        public static IValueConverter BtcToAmountUI { get; } = new BtcToAmountUIConverter();
        public static IValueConverter BtcToAmountUIWithDefault { get; } = new BtcToAmountUIWithDefaultConverter();

        private sealed class FallbackIfNullOrEmptyConverter : IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                string fallback = parameter?.ToString() ?? string.Empty;
                return value is string s && !string.IsNullOrWhiteSpace(s) ? s : fallback;
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return value;
            }
        }

        private class SatsToBtcConverter : IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value == AvaloniaProperty.UnsetValue)
                {
                    return null;
                }

                if (value is null)
                {
                    return null;
                }

                long btc = System.Convert.ToInt64(value);
                return btc / 100_000_000m;
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value == AvaloniaProperty.UnsetValue)
                {
                    return null;
                }

                return Result.Try(() => System.Convert.ToDecimal(value))
                             .Map(sats => (decimal?)(sats * 100_000_000m))
                             .GetValueOrDefault();
            }
        }

        private class AddTodayToMonthsConverter : IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value == AvaloniaProperty.UnsetValue || value is null)
                {
                    return null;
                }

                if (value is DateTime date)
                {
                    DateTime today = DateTime.Today;
                    int months = (date.Year - today.Year) * 12 + date.Month - today.Month;

                    if (months < 0 || date.Date != today.AddMonths(months).Date)
                    {
                        return null;
                    }

                    return months;
                }

                return null;
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value == AvaloniaProperty.UnsetValue || value is null)
                {
                    return null;
                }

                if (value is int months)
                {
                    return DateTime.Today.AddMonths(months);
                }

                return null;
            }
        }

        private class RatioToPercentageConverter : IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value is decimal v)
                {
                    return v * 100;
                }
                return null;
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value is decimal v)
                {
                    return v / 100m;
                }
                return null;
            }
        }

        /// <summary>
        /// Converts between decimal? (BTC value for NumericUpDown) and IAmountUI?.
        /// Convert: IAmountUI? → decimal? (extracts Btc)
        /// ConvertBack: decimal? → IAmountUI? (creates new AmountUI from BTC)
        /// </summary>
        private class BtcToAmountUIConverter : IValueConverter
        {
            protected const long SatsPerBtc = 100_000_000;

            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value is IAmountUI amount)
                {
                    return amount.Btc;
                }
                return null;
            }

            public virtual object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value is decimal btc)
                {
                    long sats = (long)(btc * SatsPerBtc);
                    return new AmountUI(sats);
                }
                return null;
            }
        }

        /// <summary>
        /// Same as BtcToAmountUIConverter but defaults to 0.001 BTC when value is null/cleared.
        /// Used for Threshold inputs where a default is desired.
        /// </summary>
        private class BtcToAmountUIWithDefaultConverter : BtcToAmountUIConverter
        {
            private const long DefaultThresholdSats = 100_000; // 0.001 BTC

            public override object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value is decimal btc)
                {
                    long sats = (long)(btc * SatsPerBtc);
                    return new AmountUI(sats);
                }
                // When null/empty, return the default threshold
                return new AmountUI(DefaultThresholdSats);
            }
        }

        public static IValueConverter IsEqualTo { get; } = new EqualsConverter();

        private class EqualsConverter : IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return object.Equals(value, parameter);
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return AvaloniaProperty.UnsetValue;
            }
        }

        public static IValueConverter JoinStrings { get; } = new FuncValueConverter<IEnumerable<int>?, string?>(
            values => values == null ? null : string.Join(", ", values));

        public static IValueConverter ThicknessWithoutTop { get; } = new FuncValueConverter<Thickness, Thickness>(
            thickness => new Thickness(thickness.Left, 0, thickness.Right, thickness.Bottom));

        public static IValueConverter DialogOptionsRows { get; } = new FuncValueConverter<int, int>(
            itemCount => itemCount <= 2 ? 1 : 0);

        public static IValueConverter DialogOptionsColumns { get; } = new FuncValueConverter<int, int>(
            itemCount => itemCount <= 2 ? 0 : 1);
    }
}

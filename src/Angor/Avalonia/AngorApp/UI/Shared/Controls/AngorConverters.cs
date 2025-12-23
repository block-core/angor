using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using Angor.Shared.Utilities;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;
using Humanizer;
using Microsoft.Extensions.Logging.Abstractions;

namespace AngorApp.UI.Shared.Controls;

public static class AngorConverters
{
    private static readonly NostrConversionHelper NostrConversionHelper = new(new NullLogger<NostrConversionHelper>());
    public static readonly FuncValueConverter<DateTimeOffset, string> TimeLeftDateTimeOffset = new(offset => { return offset.Humanize(dateToCompareAgainst: DateTimeOffset.Now); });
    public static readonly FuncValueConverter<DateTime, string> TimeLeftDateTime = new(offset => { return offset.Humanize(dateToCompareAgainst: DateTime.Now); });

    public static readonly FuncValueConverter<TimeSpan, string> HumanizeTimeSpan = new(offset => offset.Humanize());

    public static readonly FuncValueConverter<DateTimeOffset, string> HumanizeDateTimeOffset = new(offset =>
    {
        if (DateTimeOffset.Now.Date - offset < 2.Days())
        {
            return offset.Humanize();
        }

        return offset.ToString("d");
    });

    public static readonly FuncValueConverter<DateTime?, string> HumanizeDateTime = new(offset =>
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
        var label = value == 1 ? "month" : "month".Pluralize();
        return label.Humanize(LetterCasing.Title);
    });

    public static FuncValueConverter<string, SvgImage> StringToQRCode { get; } = new(s =>
    {
        Debug.Assert(s != null, nameof(s) + " != null");
        return QRGenerator.SvgImageFrom(s);
    });

    public static FuncValueConverter<long, decimal> SatsToBtc { get; } = new(satoshis =>
    {
        var btc = satoshis / (decimal)1_0000_0000;
        return btc;
    });

    public static readonly FuncValueConverter<object, string> ToJson = new(obt => System.Text.Json.JsonSerializer.Serialize(obt, new JsonSerializerOptions()
    {
        WriteIndented = true,
    }));

    public static IValueConverter HexToNpub { get;} = new FuncValueConverter<string?, string?>(s => s == null ? null : NostrConversionHelper.ConvertHexToNpub(s));

    public static IValueConverter FallbackIfNullOrEmpty { get; } = new FallbackIfNullOrEmptyConverter();

    private sealed class FallbackIfNullOrEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var fallback = parameter?.ToString() ?? string.Empty;
            return value is string s && !string.IsNullOrWhiteSpace(s) ? s : fallback;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
}

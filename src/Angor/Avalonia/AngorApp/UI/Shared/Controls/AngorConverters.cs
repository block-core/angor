using System.Diagnostics;
using System.Text.Json;
using Angor.Contests.CrossCutting;
using Angor.Shared.Utilities;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Svg;
using Avalonia.Svg.Skia;
using Humanizer;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Projektanker.Icons.Avalonia;
using Zafiro.UI.Navigation.Sections;

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
}
using System.Diagnostics;
using AngorApp.Sections.Shell;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Svg;
using Humanizer;
using Humanizer.DateTimeHumanizeStrategy;
using Projektanker.Icons.Avalonia;
using Separator = AngorApp.Sections.Shell.Separator;

namespace AngorApp.Controls;

public static class MiscConverters
{
    public static readonly FuncValueConverter<string, object> StringToIcon = new(str =>
    {
        if (str is null)
        {
            return AvaloniaProperty.UnsetValue;
        }

        var prefix = str.Split(":");
        if (prefix[0] == "svg")
        {
            return new Avalonia.Svg.Svg(new Uri("avares://AngorApp"))
            {
                Path = prefix[1]
            };
        }

        return new Icon
        {
            Value = str
        };
    });

    public static readonly FuncValueConverter<SectionBase, bool> IsActivatable = new(sectionBase => sectionBase is not Separator);

    public static readonly FuncValueConverter<bool, Dock> IsPrimaryToDock = new(isPrimary => isPrimary ? Dock.Top : Dock.Bottom);
    
    public static readonly FuncValueConverter<DateTimeOffset, string> TimeLeft = new(offset =>
    {
        return offset.Humanize(dateToCompareAgainst: DateTimeOffset.Now);
    });
    
    public static readonly FuncValueConverter<TimeSpan, string> HumanizeTimeSpan = new(offset => offset.Humanize());

    public static readonly FuncValueConverter<bool, double> BoolToOpacity = new(b => b ? 1 : 0);

    public static FuncValueConverter<string, string> HubProfile = new((value) =>
    {
        return "https://hub.angor.io/profile/" + value;
    });

    public static string BigBtcFormat = "{0} BTC";
    public static string AmountBtcFormat = "0.0000 0000 BTC";

    public static FuncValueConverter<string, SvgImage> StringToQRCode { get; } = new(s =>
    {
        Debug.Assert(s != null, nameof(s) + " != null");
        return QRGenerator.SvgImageFrom(s);
    });

    public static FuncValueConverter<ulong, string> SatsToBtc { get; } = new(satoshis =>
    {
        var btc = satoshis / 10000_0000;
        return $"{btc:0.0000 0000}" + " BTC";
    });
}
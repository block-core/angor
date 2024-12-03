using System;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Svg;
using Projektanker.Icons.Avalonia;
using Svg.Model;

namespace AngorApp;

public static class IconConverters
{
    public static readonly FuncValueConverter<string, object> StringToIcon = new FuncValueConverter<string, object>(str =>
    {
        if (str is null)
        {
            return AvaloniaProperty.UnsetValue;
        }

        var prefix = str.Split(":");
        if (prefix[0] == "svg")
        {
            return new SvgImage()
            {
                Source = SvgSource.Load(prefix[1], new Uri("avares://AngorApp"), new SvgParameters())
            };
        }
        
        return new Icon()
        {
            Value = str,
        };
    });
}
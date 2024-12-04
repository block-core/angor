using System;
using AngorApp.Sections.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Projektanker.Icons.Avalonia;

namespace AngorApp;

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

    public static readonly FuncValueConverter<SectionBase, bool> IsSection = new(sectionBase => sectionBase is Section);

    public static readonly FuncValueConverter<bool, Dock> IsPrimaryToDock = new(isPrimary => isPrimary ? Dock.Top : Dock.Bottom);
}
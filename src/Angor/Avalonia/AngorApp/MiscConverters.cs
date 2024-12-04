using System;
using AngorApp.Sections.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Projektanker.Icons.Avalonia;
using Separator = AngorApp.Sections.Shell.Separator;

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

    public static readonly FuncValueConverter<SectionBase, bool> IsActivatable = new(sectionBase => sectionBase is not Separator);

    public static readonly FuncValueConverter<bool, Dock> IsPrimaryToDock = new(isPrimary => isPrimary ? Dock.Top : Dock.Bottom);
}
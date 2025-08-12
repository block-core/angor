using System.Xml.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Zafiro.Avalonia;
using Zafiro.Reactive;

namespace AngorApp.UI;

public class AngorIconConverter : IIconConverter
{
    public static AngorIconConverter Instance { get; } = new();
    
    public Control? Convert(Zafiro.UI.IIcon icon)
    {
        // 1. División en dos partes: esquema y resto
        var parts = icon.Source.Split(new[] { ':' }, 2);
        if (parts.Length != 2 || parts[0] != "svg")
            return new Projektanker.Icons.Avalonia.Icon() { Value = icon.Source };

        var remainder = parts[1];
        string assemblyName;
        string resourcePath;

        // 2. Formato implícito: /ruta → ensamblado actual
        if (remainder.StartsWith("/"))
        {
            assemblyName = Application.Current!.GetType().Assembly.GetName().Name!;
            resourcePath = remainder.TrimStart('/');
        }
        else
        {
            // 3. Formato explícito: NombreEnsamblado/ruta
            var idx = remainder.IndexOf('/');
            if (idx <= 0)
                return new Projektanker.Icons.Avalonia.Icon { Value = icon.Source }; // formato inválido

            assemblyName = remainder[..idx];
            resourcePath = remainder[(idx + 1)..];
        }

        var uri = new Uri($"avares://{assemblyName}");

        var isLight = Application.Current?.ActualThemeVariant == ThemeVariant.Light;
        var color = isLight ? "Black" : "White";
        var svgContents = AssetLoader.Open(new Uri(uri, resourcePath)).ReadToEnd().Result;
        var transformer = new AngorSvgTransformer(color);
        var final = transformer.Transform(svgContents);

        return new global::Avalonia.Svg.Skia.Svg(uri) { Source = final };
    }
}

public class AngorSvgTransformer(string color)
{
    public string Transform(string svgContent)
    {
        var svg = XElement.Parse(svgContent);

        foreach (var element in svg.Descendants())
        {
            var strokeAttribute = element.Attribute("stroke");
            if (strokeAttribute != null)
            {
                strokeAttribute.Value = color;
            }

            var fillAttribute = element.Attribute("fill");
            if (fillAttribute != null)
            {
                fillAttribute.Value = color;
            }
        }

        return svg.ToString();
    }
}
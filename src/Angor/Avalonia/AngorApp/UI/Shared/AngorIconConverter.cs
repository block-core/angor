using System.Xml.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Zafiro.Avalonia;
using Zafiro.Reactive;

namespace AngorApp.UI.Shared;

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

        return new ThemeAwareSvgIcon(new Uri(uri, resourcePath));
    }

    private sealed class ThemeAwareSvgIcon(Uri uri) : global::Avalonia.Svg.Skia.Svg(uri)
    {
        private IDisposable? subscription;
        private readonly Uri Uri = uri;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (Application.Current != null)
            {
                UpdateSource(Application.Current.ActualThemeVariant);
            }

            subscription ??= this.GetObservable(ThemeVariantScope.ActualThemeVariantProperty)
                .Subscribe(UpdateSource);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            subscription?.Dispose();
            subscription = null;
        }

        private void UpdateSource(ThemeVariant themeVariant)
        {
            var contents = AssetLoader.Open(this.Uri).ReadToEnd().Result;
            var color = themeVariant == ThemeVariant.Light ? "Black" : "White";
            Source = AngorSvgTransformer.Transform(contents, color);
        }
    }
}

public static class AngorSvgTransformer
{
    public static string Transform(string svgContent, string color)
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
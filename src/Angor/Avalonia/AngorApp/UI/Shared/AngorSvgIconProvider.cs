using System.IO;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using Zafiro.Avalonia.Icons;
using Zafiro.UI;

namespace AngorApp.UI.Shared;

public class AngorSvgIconProvider : IIconControlProvider
{
    public string Prefix => "svg";

    public Control? Create(IIcon icon, string valueWithoutPrefix)
    {
        var remainder = valueWithoutPrefix;
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
            {
                // Invalid format for this provider
                return null;
            }

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
            using var stream = AssetLoader.Open(this.Uri);
            using var reader = new StreamReader(stream);
            var contents = reader.ReadToEnd();
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

using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;
using QRCoder;

namespace App.UI.Shared;

/// <summary>
/// Converts a string (address/invoice) to an SVG QR code image.
/// Returns null for null/empty input so the placeholder remains visible.
/// </summary>
public class QRCodeConverter : IValueConverter
{
    public static readonly QRCodeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return null;

        try
        {
            using var stream = SvgStreamFor(s);
            var svgImage = new SvgImage
            {
                Source = SvgSource.LoadFromStream(stream)
            };
            return svgImage;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static MemoryStream SvgStreamFor(string content)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(qrCodeData);
        var qrCodeSvg = qrCode.GetGraphic(20);

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(qrCodeSvg);
        writer.Flush();
        stream.Position = 0;

        return stream;
    }
}

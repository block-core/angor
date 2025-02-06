using System.IO;
using Avalonia.Svg;
using QRCoder;

namespace AngorApp.UI.Controls;

public static class QRGenerator
{
    public static SvgImage SvgImageFrom(string content)
    {
        using var stream = SvgStreamFor(content);
        var svgImage = new SvgImage
        {
            Source = SvgSource.Load(stream)
        };
        
        return svgImage;
    }

    public static Stream SvgStreamFor(string content)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(qrCodeData);
        string qrCodeSvg = qrCode.GetGraphic(20);
    
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(qrCodeSvg);
        writer.Flush();
        stream.Position = 0;
    
        return stream;
    }
}
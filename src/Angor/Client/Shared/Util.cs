using Angor.Client.Storage;
using Angor.Shared.Models;
using Microsoft.AspNetCore.Components;
using QRCoder;

namespace Angor.Client.Shared
{
    public class Util
    {
        public static string GenerateQRCode(string content)
        {
            using QRCodeGenerator qrGenerator = new QRCodeGenerator();
            using QRCodeData qrCodeData = qrGenerator.CreateQrCode("The text which should be encoded.", QRCodeGenerator.ECCLevel.Q);
            using PngByteQRCode pngByteQRCode = new PngByteQRCode(qrCodeData);
            return Convert.ToBase64String(pngByteQRCode.GetGraphic(10));
        }

    }
}

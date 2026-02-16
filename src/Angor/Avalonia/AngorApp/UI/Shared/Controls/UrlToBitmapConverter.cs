using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.Net.Http;

namespace AngorApp.UI.Shared.Controls;

public class UrlToBitmapConverter : IValueConverter
{
    private static readonly HttpClient httpClient = new HttpClient();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string url && !string.IsNullOrEmpty(url)) {
            try {
                // For web URLs
                if (url.StartsWith("http://") || url.StartsWith("https://")) {
                    var data = httpClient.GetByteArrayAsync(url).Result;
                    using var stream = new System.IO.MemoryStream(data);
                    return new Bitmap(stream);
                }

                // For local resources
                return new Bitmap(url);
            }
            catch {
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

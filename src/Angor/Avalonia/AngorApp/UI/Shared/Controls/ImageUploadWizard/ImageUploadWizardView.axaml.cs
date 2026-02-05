using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AngorApp.UI.Shared.Controls.ImageUploadWizard;

public partial class ImageUploadWizardView : UserControl
{
    public static readonly IValueConverter FileSizeConverter = new FileSizeValueConverter();

    public ImageUploadWizardView()
    {
        InitializeComponent();
    }

    private class FileSizeValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return ImageUploadWizardViewModel.FormatFileSize(bytes);
            }
            return "0 B";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

/// <summary>
/// Converter that returns different text based on boolean value.
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "True";
    public string FalseText { get; set; } = "False";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueText : FalseText;
        }
        return FalseText;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converter that returns different brushes based on boolean value.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public IBrush? TrueBrush { get; set; }
    public IBrush? FalseBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueBrush : FalseBrush;
        }
        return FalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

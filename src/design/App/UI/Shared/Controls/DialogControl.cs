using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;

namespace App.UI.Shared.Controls;

/// <summary>
/// Represents a dialog option with role-based styling.
/// Replaces Zafiro's IOption interface.
/// </summary>
public interface IOption
{
    IObservable<bool> IsVisible { get; }
    bool IsCancel { get; }
    bool IsDefault { get; }
    OptionRole Role { get; }
    IObservable<string?> Title { get; }
    ICommand Command { get; }
}

/// <summary>
/// Dialog option roles for button styling.
/// </summary>
public enum OptionRole
{
    Primary,
    Secondary,
    Cancel,
    Destructive
}

/// <summary>
/// Design-time implementation of IOption for XAML previews.
/// Replaces Zafiro's OptionDesign.
/// </summary>
public class OptionDesign : IOption
{
    private string? _title;
    
    public IObservable<bool> IsVisible { get; set; } = Observable.Return(true);
    public bool IsCancel { get; set; }
    public bool IsDefault { get; set; }
    public OptionRole Role { get; set; }
    
    /// <summary>
    /// XAML-friendly Title property. Setting this updates the observable.
    /// </summary>
    public string? Title
    {
        get => _title;
        set => _title = value;
    }
    
    // The IOption interface expects IObservable<string?>
    IObservable<string?> IOption.Title => Observable.Return(_title);
    
    public ICommand Command { get; set; } = ReactiveCommand.Create(() => { });
}

/// <summary>
/// Converts OptionRole to a simple string for class-based styling.
/// Replaces Zafiro's OptionRoleToButtonRoleConverter.
/// </summary>
public class OptionRoleToStringConverter : IValueConverter
{
    public static readonly OptionRoleToStringConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is OptionRole role ? role.ToString() : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts OptionRole to the CSS class name used by the button style system.
/// Primary → "Emphasized", Secondary → "Secondary", Cancel → "Outline", Destructive → "Destructive".
/// </summary>
public class OptionRoleToClassNameConverter : IValueConverter
{
    public static readonly OptionRoleToClassNameConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is OptionRole role ? role switch
        {
            OptionRole.Primary => "Emphasized",
            OptionRole.Secondary => "Secondary",
            OptionRole.Cancel => "Outline",
            OptionRole.Destructive => "Destructive",
            _ => null
        } : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// A dialog content control with options (action buttons).
/// Replaces Zafiro's DialogControl.
/// </summary>
public class DialogControl : ContentControl
{
    public static readonly StyledProperty<IList?> OptionsProperty =
        AvaloniaProperty.Register<DialogControl, IList?>(nameof(Options));

    public IList? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }
}

/// <summary>
/// A modal dialog container with title, close button, and content.
/// Replaces Zafiro's DialogViewContainer.
/// </summary>
public class DialogViewContainer : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DialogViewContainer, string?>(nameof(Title));

    public static readonly StyledProperty<ICommand?> CloseProperty =
        AvaloniaProperty.Register<DialogViewContainer, ICommand?>(nameof(Close));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ICommand? Close
    {
        get => GetValue(CloseProperty);
        set => SetValue(CloseProperty, value);
    }
}

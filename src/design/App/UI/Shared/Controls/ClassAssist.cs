using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace App.UI.Shared.Controls;

/// <summary>
/// Attached properties that apply space-separated CSS class names to a control
/// or its first child. Replaces Zafiro.Avalonia.Misc.ClassAssist.
/// </summary>
public static class ClassAssist
{
    /// <summary>
    /// Applies space-separated CSS class names directly to the attached control.
    /// Usage: ClassAssist.Classes="Card LightGray"
    /// </summary>
    public static readonly AttachedProperty<string?> ClassesProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Classes", typeof(ClassAssist));

    /// <summary>
    /// Applies space-separated CSS class names to the first child of the attached control.
    /// Usage: ClassAssist.ChildClasses="Weight-Bold"
    /// </summary>
    public static readonly AttachedProperty<string?> ChildClassesProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ChildClasses", typeof(ClassAssist));

    static ClassAssist()
    {
        ClassesProperty.Changed.AddClassHandler<Control>(OnClassesChanged);
        ChildClassesProperty.Changed.AddClassHandler<Control>(OnChildClassesChanged);
    }

    public static string? GetClasses(Control element) => element.GetValue(ClassesProperty);
    public static void SetClasses(Control element, string? value) => element.SetValue(ClassesProperty, value);

    public static string? GetChildClasses(Control element) => element.GetValue(ChildClassesProperty);
    public static void SetChildClasses(Control element, string? value) => element.SetValue(ChildClassesProperty, value);

    private static void OnClassesChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        ApplyClasses(control, e.OldValue as string, e.NewValue as string);
    }

    private static void OnChildClassesChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        // For ContentPresenter / ContentControl, apply to the presented child
        // We defer to when the control is attached to the visual tree
        if (control is ContentPresenter presenter)
        {
            void Handler(object? sender, EventArgs args)
            {
                if (presenter.Child is Control child)
                {
                    ApplyClasses(child, e.OldValue as string, e.NewValue as string);
                }
            }

            presenter.AttachedToVisualTree -= Handler;
            presenter.AttachedToVisualTree += Handler;

            // Also apply immediately if already attached
            if (presenter.Child is Control existingChild)
            {
                ApplyClasses(existingChild, e.OldValue as string, e.NewValue as string);
            }
        }
    }

    private static void ApplyClasses(Control control, string? oldClasses, string? newClasses)
    {
        if (!string.IsNullOrWhiteSpace(oldClasses))
        {
            foreach (var cls in oldClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                control.Classes.Remove(cls);
            }
        }

        if (!string.IsNullOrWhiteSpace(newClasses))
        {
            foreach (var cls in newClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!control.Classes.Contains(cls))
                {
                    control.Classes.Add(cls);
                }
            }
        }
    }
}

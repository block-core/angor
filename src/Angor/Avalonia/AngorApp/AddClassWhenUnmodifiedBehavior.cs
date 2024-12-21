using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace AngorApp;

public class AddClassWhenUnmodifiedBehavior : Behavior<Control>
{
    private bool hasBeenModified = false;
    private IDisposable? textSubscription;

    public static readonly StyledProperty<string> ClassNameProperty = AvaloniaProperty.Register<AddClassWhenUnmodifiedBehavior, string>(
        nameof(ClassName), "initial");

    public string ClassName
    {
        get => GetValue(ClassNameProperty);
        set => SetValue(ClassNameProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        
        if (AssociatedObject != null)
        {
            AssociatedObject.Classes.Add(ClassName);
            
            var textProperty = AssociatedObject switch
            {
                NumericUpDown => NumericUpDown.TextProperty,
                TextBox => TextBox.TextProperty,
                _ => null
            };

            if (textProperty != null)
            {
                textSubscription = AssociatedObject.GetObservable(textProperty)
                    .Subscribe(_ => OnTextChanged());
            }
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        
        if (AssociatedObject != null)
        {
            textSubscription?.Dispose();
            if (AssociatedObject.GetVisualRoot() is {})
            {
                AssociatedObject.Classes.Remove(ClassName);
            }
        }
    }

    private void OnTextChanged()
    {
        if (!hasBeenModified && AssociatedObject?.GetVisualRoot() is {})
        {
            hasBeenModified = true;
            AssociatedObject.Classes.Remove(ClassName);
        }
    }
}
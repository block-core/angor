using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.VisualTree;

namespace AngorApp.Sections.Founder
{
    public class ContainerIndexBehavior
    {
        // 1-based index attached property
        public static readonly AttachedProperty<int> ItemIndexProperty =
            AvaloniaProperty.RegisterAttached<ContainerIndexBehavior, Control, int>(
                "ItemIndex",
                defaultValue: -1);

        public static int GetItemIndex(Control control) =>
            control.GetValue(ItemIndexProperty);

        // Flag to start tracking
        public static readonly AttachedProperty<bool> TrackIndexProperty =
            AvaloniaProperty.RegisterAttached<ContainerIndexBehavior, Control, bool>(
                "TrackIndex",
                defaultValue: false);

        public static void SetTrackIndex(Control control, bool value) =>
            control.SetValue(TrackIndexProperty, value);

        // Static ctor: hook up class-wide listener for TrackIndex changes
        static ContainerIndexBehavior()
        {
            // English: Subscribe a class handler to TrackIndexProperty changes
            TrackIndexProperty.Changed.AddClassHandler<Control>(OnTrackIndexChanged);
        }

        private static void OnTrackIndexChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                control.AttachedToVisualTree   += OnAttached;
                control.DetachedFromVisualTree += OnDetached;
            }
            else
            {
                control.AttachedToVisualTree   -= OnAttached;
                control.DetachedFromVisualTree -= OnDetached;
                control.SetValue(ItemIndexProperty, -1);
            }
        }

        private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            var control = (Control)sender!;
            SubscribeCollectionChanges(control);
            UpdateIndex(control);
        }

        private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // English: reset when removed
            var control = (Control)sender!;
            control.SetValue(ItemIndexProperty, -1);
        }

        private static void SubscribeCollectionChanges(Control control)
        {
            var itemsControl = control
                .GetVisualAncestors()
                .OfType<ItemsControl>()
                .FirstOrDefault();

            if (itemsControl?.Items is INotifyCollectionChanged incc)
            {
                void Handler(object? s, NotifyCollectionChangedEventArgs _) =>
                    UpdateIndex(control);

                incc.CollectionChanged += Handler;

                control.DetachedFromVisualTree += (_, __) =>
                    incc.CollectionChanged -= Handler;
            }
        }

        private static void UpdateIndex(Control control)
        {
            var itemsControl = control
                .GetVisualAncestors()
                .OfType<ItemsControl>()
                .FirstOrDefault();

            if (itemsControl is null)
                return;

            var model = control.DataContext;
            if (model is null)
                return;

            // English: compute 0-based index
            var idx = (itemsControl.Items as IEnumerable)?
                .Cast<object>()
                .ToList()
                .IndexOf(model) ?? -1;

            if (idx >= 0)
            {
                // convert to 1-based
                control.SetValue(ItemIndexProperty, idx + 1);
            }
        }
    }
}

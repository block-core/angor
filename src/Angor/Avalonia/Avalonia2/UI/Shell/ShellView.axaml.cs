using System.Globalization;
using System.Reactive.Linq;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace Avalonia2.UI.Shell;

/// <summary>
/// Converts a StreamGeometry resource key (e.g. "NavIconHome") to the actual StreamGeometry instance.
/// Used in the nav item DataTemplate to bind icon paths.
/// </summary>
public class NavIconConverter : IValueConverter
{
    public static readonly NavIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current!.TryFindResource(key, out var resource) && resource is StreamGeometry geometry)
        {
            return geometry;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public partial class ShellView : UserControl
{
    /// <summary>Heavy blur for modal backdrop — much more prominent than before.</summary>
    private static readonly BlurEffect ModalBlur = new() { Radius = 20 };

    /// <summary>Animation duration matching Vue prototype modal-fade: 250ms.</summary>
    private static readonly TimeSpan AnimDuration = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Shared transitions applied to modal content controls for open/close animation.
    /// Uses TransformOperationsTransition for scale + DoubleTransition for opacity.
    /// </summary>
    private static readonly Transitions ModalTransitions = new()
    {
        new TransformOperationsTransition
        {
            Property = RenderTransformProperty,
            Duration = AnimDuration,
            Easing = new CubicEaseOut(),
        },
        new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = AnimDuration,
            Easing = new CubicEaseOut(),
        },
    };

    /// <summary>Transitions for the backdrop border opacity fade.</summary>
    private static readonly Transitions BackdropTransitions = new()
    {
        new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = AnimDuration,
            Easing = new CubicEaseOut(),
        },
    };

    /// <summary>Transitions for the toast fade animation (Vue: 0.3s ease).</summary>
    private static readonly Transitions ToastTransitions = new()
    {
        new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new CubicEaseOut(),
        },
    };

    /// <summary>
    /// The current modal content control that has been added as a direct child
    /// of the ModalOverlay Panel. We track it so we can remove it on close.
    /// </summary>
    private Control? _currentModalChild;

    /// <summary>Guard to prevent re-entrant close animation.</summary>
    private bool _isClosing;

    public ShellView()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<ShellViewModel>();
        DataContext = vm;

        var modalOverlay = this.FindControl<Panel>("ModalOverlay")!;
        var shellContent = this.FindControl<Grid>("ShellContent")!;
        var backdrop = this.FindControl<Border>("ShellModalBackdrop")!;

        // Apply backdrop transitions once
        backdrop.Transitions = BackdropTransitions;

        // React to ModalContent changes — manage the visual tree directly.
        // This replaces the XAML ContentPresenter binding which suffered from
        // an intermittent race: IsVisible and Content changing in the same
        // binding batch could cause Avalonia to skip the measure/arrange pass
        // for the new content, leaving the modal card invisible (especially
        // on second open, and more often in light mode).
        vm.WhenAnyValue(x => x.ModalContent)
            .Subscribe(content =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (content is Control control)
                    {
                        // ── OPEN ──
                        // Remove any previous modal child immediately (no close anim if replaced)
                        if (_currentModalChild != null)
                        {
                            _currentModalChild.Transitions = null;
                            modalOverlay.Children.Remove(_currentModalChild);
                            _currentModalChild = null;
                        }
                        _isClosing = false;

                        // Start at closed state: invisible + slightly scaled down
                        control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                        control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                        control.Opacity = 0;
                        control.RenderTransformOrigin = Avalonia.RelativePoint.Center;
                        control.RenderTransform = TransformOperations.Parse("scale(0.95)");

                        // Add transitions BEFORE adding to tree so Avalonia picks them up
                        control.Transitions = ModalTransitions;

                        // Backdrop starts invisible
                        backdrop.Opacity = 0;

                        // Add to visual tree
                        modalOverlay.Children.Add(control);
                        _currentModalChild = control;

                        // Make the overlay visible
                        modalOverlay.IsVisible = true;
                        shellContent.Effect = ModalBlur;

                        // Force layout so the initial state is rendered
                        modalOverlay.InvalidateMeasure();
                        modalOverlay.InvalidateArrange();

                        // Kick off open animation by setting target values on next frame
                        // (must be deferred so the initial state is committed first)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            control.Opacity = 1;
                            control.RenderTransform = TransformOperations.Parse("scale(1)");
                            backdrop.Opacity = 1;
                        }, Avalonia.Threading.DispatcherPriority.Render);
                    }
                    else
                    {
                        // ── CLOSE ──
                        if (_currentModalChild != null && !_isClosing)
                        {
                            _isClosing = true;
                            var closingChild = _currentModalChild;

                            // Animate to closed state
                            closingChild.Opacity = 0;
                            closingChild.RenderTransform = TransformOperations.Parse("scale(0.95)");
                            backdrop.Opacity = 0;

                            // Wait for transition to finish, then clean up
                            _ = CleanupAfterClose(closingChild, modalOverlay, shellContent);
                        }
                        else if (_currentModalChild == null)
                        {
                            // Nothing to animate, just hide
                            modalOverlay.IsVisible = false;
                            shellContent.Effect = null;
                        }
                    }
                });
            });

        // Shell-level backdrop click-to-close
        backdrop.PointerPressed += OnBackdropPressed;

        // ── Toast notification animation ──
        // React to ToastMessage changes: fade in when set, fade out when cleared.
        var toastBorder = this.FindControl<Border>("ToastOverlay")!;
        toastBorder.Transitions = ToastTransitions;

        vm.WhenAnyValue(x => x.ToastMessage)
            .Subscribe(message =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        // Show: make visible at opacity 0, then animate to 1
                        toastBorder.Opacity = 0;
                        toastBorder.IsVisible = true;
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            toastBorder.Opacity = 1;
                        }, Avalonia.Threading.DispatcherPriority.Render);
                    }
                    else
                    {
                        // Hide: animate opacity to 0, then set IsVisible=false after transition
                        toastBorder.Opacity = 0;
                        _ = HideToastAfterFadeAsync(toastBorder);
                    }
                });
            });
    }

    /// <summary>
    /// Wait for the close transition to finish, then remove the modal from the tree.
    /// Only hides the overlay/blur if no new modal was opened in the meantime
    /// (i.e., multi-step modal flows where ShowModal is called right after HideModal).
    /// </summary>
    private async Task CleanupAfterClose(Control closingChild, Panel modalOverlay, Grid shellContent)
    {
        // Wait for the transition duration + small buffer
        await Task.Delay(AnimDuration + TimeSpan.FromMilliseconds(50));

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            closingChild.Transitions = null;
            modalOverlay.Children.Remove(closingChild);

            // Only tear down the overlay if no replacement modal was opened
            if (_currentModalChild == closingChild || _currentModalChild == null)
            {
                _currentModalChild = null;
                modalOverlay.IsVisible = false;
                shellContent.Effect = null;
            }
            _isClosing = false;
        });
    }

    /// <summary>
    /// Wait for the toast fade-out transition (300ms), then hide the border.
    /// If a new toast appeared in the meantime (Opacity went back to 1), skip hiding.
    /// </summary>
    private static async Task HideToastAfterFadeAsync(Border toastBorder)
    {
        await Task.Delay(350); // 300ms transition + 50ms buffer
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (toastBorder.Opacity < 0.01)
                toastBorder.IsVisible = false;
        });
    }

    /// <summary>
    /// Backdrop click — close the modal. Individual modal content views handle
    /// their own close logic via OnBackdropCloseRequested if they need custom behavior.
    /// </summary>
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShellViewModel vm && vm.IsModalOpen)
        {
            // Notify the modal content that a backdrop close was requested.
            // The content can handle cleanup (e.g., resetting VM state) via IBackdropCloseable.
            if (vm.ModalContent is IBackdropCloseable closeable)
            {
                closeable.OnBackdropCloseRequested();
            }
            vm.HideModal();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Navigates to the Settings section when the header gear icon is clicked.
    /// </summary>
    private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
        {
            vm.NavigateToSettings();
        }
    }

    /// <summary>
    /// Opens the wallet switcher modal when the header wallet button is clicked.
    /// Vue: showWalletModal = true on wallet-selector-header click.
    /// </summary>
    private void OnWalletSwitcherClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm && !vm.IsModalOpen)
        {
            var modal = new WalletSwitcherModal { DataContext = vm };
            vm.ShowModal(modal);
        }
    }

    /// <summary>
    /// Called when the ListBox prepares a container for an item.
    /// Applies the NavGroupHeaderItem theme to group header entries
    /// so they are non-selectable and visually distinct.
    /// </summary>
    public void OnNavContainerPreparing(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem item && item.DataContext is NavGroupHeader)
        {
            if (this.TryFindResource("NavGroupHeaderItem", out var theme) && theme is ControlTheme ct)
            {
                item.Theme = ct;
            }
        }
    }
}

/// <summary>
/// Interface for modal content views that need custom behavior when the backdrop is clicked.
/// </summary>
public interface IBackdropCloseable
{
    void OnBackdropCloseRequested();
}

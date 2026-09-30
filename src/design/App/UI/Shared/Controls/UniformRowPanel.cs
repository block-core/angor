using Avalonia;
using Avalonia.Controls;

namespace App.UI.Shared.Controls;

/// <summary>
/// Lays out children in a single row of <see cref="ColumnCount"/> equal-width columns.
/// Used as the ItemsPanel of <em>row</em> collections inside a VirtualizingStackPanel —
/// the stack panel virtualizes/recycles rows, this panel only ever measures one row's
/// worth of cards (a handful), so it needs no virtualization itself.
/// </summary>
public class UniformRowPanel : Panel
{
    public static readonly StyledProperty<int> ColumnCountProperty =
        AvaloniaProperty.Register<UniformRowPanel, int>(nameof(ColumnCount), 1);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<UniformRowPanel, double>(nameof(ColumnSpacing), 16);

    public int ColumnCount
    {
        get => GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Grid column formula shared with the row chunker: how many columns of at least
    /// <paramref name="minItemWidth"/> fit in <paramref name="width"/> (>= 1, <= maxColumns).
    /// Mirrors ResponsiveGrid.GetColumnCount so all responsive grids agree on breakpoints.
    /// </summary>
    public static int ColumnCountForWidth(double width, double minItemWidth, double columnSpacing, int maxColumns = int.MaxValue)
    {
        if (width <= 0 || minItemWidth <= 0)
            return 1;

        var cols = (int)((width + columnSpacing) / (minItemWidth + columnSpacing));
        return Math.Max(1, Math.Min(cols, maxColumns));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var cols = Math.Max(1, ColumnCount);
        var itemWidth = Math.Max(0, (availableSize.Width - (cols - 1) * ColumnSpacing) / cols);

        double maxHeight = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
        }

        return new Size(availableSize.Width, maxHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cols = Math.Max(1, ColumnCount);
        var itemWidth = Math.Max(0, (finalSize.Width - (cols - 1) * ColumnSpacing) / cols);

        var colIndex = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            child.Arrange(new Rect(colIndex * (itemWidth + ColumnSpacing), 0, itemWidth, child.DesiredSize.Height));
            colIndex++;
        }

        return finalSize;
    }
}

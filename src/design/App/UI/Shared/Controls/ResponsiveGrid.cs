using Avalonia;
using Avalonia.Controls;

namespace App.UI.Shared.Controls;

/// <summary>
/// A responsive grid panel that auto-calculates column count based on available width.
/// Emulates CSS grid: grid-template-columns: repeat(auto-fill, minmax(MinItemWidth, 1fr)).
/// Each item stretches equally to fill the row.
/// </summary>
public class ResponsiveGrid : Panel
{
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<ResponsiveGrid, double>(nameof(MinItemWidth), 300);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<ResponsiveGrid, double>(nameof(ColumnSpacing), 20);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<ResponsiveGrid, double>(nameof(RowSpacing), 20);

    /// <summary>
    /// Minimum width of each item. The panel will fit as many columns as possible
    /// where each column is at least this wide.
    /// </summary>
    public double MinItemWidth
    {
        get => GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    private int GetColumnCount(double availableWidth)
    {
        if (availableWidth <= 0 || MinItemWidth <= 0)
            return 1;

        // How many columns of MinItemWidth fit? (accounting for gaps between them)
        var cols = (int)((availableWidth + ColumnSpacing) / (MinItemWidth + ColumnSpacing));
        return Math.Max(1, cols);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columnCount = GetColumnCount(availableSize.Width);
        var itemWidth = (availableSize.Width - (columnCount - 1) * ColumnSpacing) / columnCount;
        itemWidth = Math.Max(itemWidth, 0);

        double maxRowHeight = 0;
        double totalHeight = 0;
        var colIndex = 0; // tracks position within current row

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
                continue;

            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            maxRowHeight = Math.Max(maxRowHeight, child.DesiredSize.Height);
            colIndex++;

            // End of row
            if (colIndex == columnCount)
            {
                totalHeight += maxRowHeight;
                totalHeight += RowSpacing;
                maxRowHeight = 0;
                colIndex = 0;
            }
        }

        // Final partial row
        if (colIndex > 0)
            totalHeight += maxRowHeight;
        else if (totalHeight > 0)
            totalHeight -= RowSpacing; // remove trailing RowSpacing from last full row

        return new Size(availableSize.Width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnCount = GetColumnCount(finalSize.Width);
        var itemWidth = (finalSize.Width - (columnCount - 1) * ColumnSpacing) / columnCount;
        itemWidth = Math.Max(itemWidth, 0);

        double y = 0;
        double maxRowHeight = 0;
        var colIndex = 0;

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
                continue;

            var x = colIndex * (itemWidth + ColumnSpacing);
            child.Arrange(new Rect(x, y, itemWidth, child.DesiredSize.Height));
            maxRowHeight = Math.Max(maxRowHeight, child.DesiredSize.Height);
            colIndex++;

            // End of row
            if (colIndex == columnCount)
            {
                y += maxRowHeight + RowSpacing;
                maxRowHeight = 0;
                colIndex = 0;
            }
        }

        // Final partial row height
        if (colIndex > 0)
            y += maxRowHeight;

        return new Size(finalSize.Width, y);
    }
}

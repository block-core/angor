using Avalonia;
using Avalonia.Controls;

namespace App.UI.Shared.Controls;

/// <summary>
/// Lays out children in rows of at most <see cref="MaxPerRow"/> items, distributing
/// them so rows are as balanced as possible (5 items → 3+2, 6 → 3+3, 4 → 2+2),
/// and stretches each row's children to share the full available width equally.
///
/// Used for preset-pill rows ("3/6/12/18/24 Months" etc.) so they divide the row
/// nicely instead of hugging their content or leaving ragged gaps (issue #920 follow-up).
/// </summary>
public class BalancedWrapPanel : Panel
{
    public static readonly StyledProperty<int> MaxPerRowProperty =
        AvaloniaProperty.Register<BalancedWrapPanel, int>(nameof(MaxPerRow), 3);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<BalancedWrapPanel, double>(nameof(ColumnSpacing), 8);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<BalancedWrapPanel, double>(nameof(RowSpacing), 8);

    public int MaxPerRow
    {
        get => GetValue(MaxPerRowProperty);
        set => SetValue(MaxPerRowProperty, value);
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

    static BalancedWrapPanel()
    {
        AffectsMeasure<BalancedWrapPanel>(MaxPerRowProperty, ColumnSpacingProperty, RowSpacingProperty);
    }

    /// <summary>Items per row, balanced: e.g. 5 items / max 3 → [3, 2].</summary>
    private int[] ComputeRows(int count)
    {
        if (count == 0) return Array.Empty<int>();
        int maxPerRow = Math.Max(1, MaxPerRow);
        int rows = (count + maxPerRow - 1) / maxPerRow;
        int baseCount = count / rows;
        int extra = count % rows;
        var result = new int[rows];
        for (int i = 0; i < rows; i++)
            result[i] = baseCount + (i < extra ? 1 : 0);
        return result;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var rows = ComputeRows(visible.Count);
        double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        double totalHeight = 0;
        int index = 0;

        foreach (int itemsInRow in rows)
        {
            double cellWidth = width > 0
                ? Math.Max(0, (width - ColumnSpacing * (itemsInRow - 1)) / itemsInRow)
                : double.PositiveInfinity;
            double rowHeight = 0;
            for (int i = 0; i < itemsInRow; i++, index++)
            {
                visible[index].Measure(new Size(cellWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, visible[index].DesiredSize.Height);
            }
            totalHeight += rowHeight;
        }

        if (rows.Length > 1)
            totalHeight += RowSpacing * (rows.Length - 1);

        return new Size(width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var rows = ComputeRows(visible.Count);
        double y = 0;
        int index = 0;

        foreach (int itemsInRow in rows)
        {
            double cellWidth = Math.Max(0, (finalSize.Width - ColumnSpacing * (itemsInRow - 1)) / itemsInRow);
            double rowHeight = 0;
            for (int i = 0; i < itemsInRow; i++)
                rowHeight = Math.Max(rowHeight, visible[index + i].DesiredSize.Height);

            double x = 0;
            for (int i = 0; i < itemsInRow; i++, index++)
            {
                visible[index].Arrange(new Rect(x, y, cellWidth, rowHeight));
                x += cellWidth + ColumnSpacing;
            }
            y += rowHeight + RowSpacing;
        }

        return finalSize;
    }
}

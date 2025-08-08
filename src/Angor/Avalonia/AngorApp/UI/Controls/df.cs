using Avalonia;

namespace AngorApp.UI.Controls;

/// <summary>
/// Wraps children into rows like a WrapPanel, but all children receive the same cell size
/// like a UniformGrid. Optionally fills each row width or keeps a preferred cell width.
/// </summary>
public class UniformWrapPanel : Panel
{
    // Spacing
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<UniformWrapPanel, double>(nameof(ColumnSpacing), 0);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<UniformWrapPanel, double>(nameof(RowSpacing), 0);

    // Fixed item size (optional)
    public static readonly StyledProperty<double?> ItemWidthProperty =
        AvaloniaProperty.Register<UniformWrapPanel, double?>(nameof(ItemWidth));

    public static readonly StyledProperty<double?> ItemHeightProperty =
        AvaloniaProperty.Register<UniformWrapPanel, double?>(nameof(ItemHeight));

    // Aspect ratio (width / height) used when one or both dimensions are not fixed
    public static readonly StyledProperty<double> ItemAspectRatioProperty =
        AvaloniaProperty.Register<UniformWrapPanel, double>(nameof(ItemAspectRatio), 1.0);

    // Upper bound for a cell width as a proportion of the available width, e.g. 0.33
    public static readonly StyledProperty<double?> MaxItemWidthRatioProperty =
        AvaloniaProperty.Register<UniformWrapPanel, double?>(nameof(MaxItemWidthRatio));

    // When true, try column counts that minimize holes in the last row
    public static readonly StyledProperty<bool> MinimizeLastRowGapsProperty =
        AvaloniaProperty.Register<UniformWrapPanel, bool>(nameof(MinimizeLastRowGaps), true);

    // When true, stretch cell width to fill the row exactly; when false, use preferred width
    public static readonly StyledProperty<bool> FillRowProperty =
        AvaloniaProperty.Register<UniformWrapPanel, bool>(nameof(FillRow), true);

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

    public double? ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double? ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double ItemAspectRatio
    {
        get => GetValue(ItemAspectRatioProperty);
        set => SetValue(ItemAspectRatioProperty, value);
    }

    public double? MaxItemWidthRatio
    {
        get => GetValue(MaxItemWidthRatioProperty);
        set => SetValue(MaxItemWidthRatioProperty, value);
    }

    public bool MinimizeLastRowGaps
    {
        get => GetValue(MinimizeLastRowGapsProperty);
        set => SetValue(MinimizeLastRowGapsProperty, value);
    }

    public bool FillRow
    {
        get => GetValue(FillRowProperty);
        set => SetValue(FillRowProperty, value);
    }

    // Private state between Measure and Arrange
    private Size _cellSize;
    private int _columns;
    private int _rows;

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Children.Count;
        if (count == 0)
            return default;

        var spacingX = ColumnSpacing;
        var spacingY = RowSpacing;

        // 1) Infer a preferred cell size
        var (prefW, prefH) = InferPreferredCellSize(availableSize);

        // 2) Apply MaxItemWidthRatio if any
        if (MaxItemWidthRatio is double ratio &&
            ratio > 0 &&
            !double.IsInfinity(availableSize.Width) &&
            availableSize.Width > 0)
        {
            var maxCellW = ratio * availableSize.Width;
            if (prefW > maxCellW)
            {
                prefW = maxCellW;
                // Derive height from aspect if height not fixed
                if (ItemHeight is null)
                    prefH = prefW / Math.Max(0.0001, ItemAspectRatio);
            }
        }

        prefW = Math.Max(0, prefW);
        prefH = Math.Max(0, prefH);

        // 3) Compute maximum feasible columns for the preferred cell width
        int maxCols = ComputeMaxColumns(availableSize.Width, prefW, spacingX);
        if (maxCols < 1) maxCols = 1;

        // 4) Choose column count
        _columns = MinimizeLastRowGaps
            ? ChooseColumnsMinimizingHoles(count, maxCols)
            : maxCols;

        // 5) Compute actual cell width depending on FillRow policy
        var cellW = ComputeCellWidth(
            availableSize.Width, _columns, spacingX, prefW, FillRow);

        // 6) Resolve cell height
        var cellH = ResolveHeight(cellW, prefH);

        _cellSize = new Size(cellW, cellH);

        // 7) Measure children with the decided cell size
        foreach (var child in Children)
            child.Measure(_cellSize);

        // 8) Compute rows and desired panel size
        _rows = (int)Math.Ceiling(count / (double)_columns);

        var totalWidth = _columns * _cellSize.Width + (_columns - 1) * spacingX;
        var totalHeight = _rows * _cellSize.Height + (_rows - 1) * spacingY;

        var desired = new Size(
            double.IsInfinity(availableSize.Width) ? totalWidth : Math.Min(availableSize.Width, totalWidth),
            double.IsInfinity(availableSize.Height) ? totalHeight : Math.Min(availableSize.Height, totalHeight));

        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = Children.Count;
        if (count == 0)
            return finalSize;

        var spacingX = ColumnSpacing;
        var spacingY = RowSpacing;

        // Recompute width for the actual final width with the same FillRow policy
        var cellW = ComputeCellWidth(
            finalSize.Width, _columns, spacingX, _cellSize.Width, FillRow);
        var cellH = ResolveHeight(cellW, _cellSize.Height);
        var cell = new Size(cellW, cellH);

        for (int i = 0; i < count; i++)
        {
            int row = i / _columns;
            int col = i % _columns;

            var x = col * (cellW + spacingX);
            var y = row * (cellH + spacingY);

            Children[i].Arrange(new Rect(new Point(x, y), cell));
        }

        // Report the area actually used
        var usedWidth = _columns * cellW + (_columns - 1) * spacingX;
        var usedHeight = _rows * cellH + (_rows - 1) * spacingY;

        return new Size(
            Math.Min(finalSize.Width, usedWidth),
            Math.Min(finalSize.Height, usedHeight));
    }

    // ===== Helpers =====

    // Decide preferred cell size from properties and children's DesiredSize
    private (double width, double height) InferPreferredCellSize(Size available)
    {
        if (ItemWidth is double iw && ItemHeight is double ih)
            return (iw, ih);

        double maxChildW = 0, maxChildH = 0;

        // Probe children's desired size only if needed
        if (ItemWidth is null || ItemHeight is null)
        {
            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var d = child.DesiredSize;
                if (d.Width > maxChildW) maxChildW = d.Width;
                if (d.Height > maxChildH) maxChildH = d.Height;
            }
        }

        if (ItemWidth is double onlyW && ItemHeight is null)
        {
            var h = Math.Max(maxChildH, onlyW / Math.Max(0.0001, ItemAspectRatio));
            return (onlyW, h);
        }

        if (ItemWidth is null && ItemHeight is double onlyH)
        {
            var w = Math.Max(maxChildW, onlyH * ItemAspectRatio);
            return (w, onlyH);
        }

        // Neither fixed: start from children's max desired and enforce aspect
        var tentativeW = double.IsInfinity(available.Width) ? maxChildW : Math.Min(maxChildW, Math.Max(1, available.Width));
        var tentativeH = tentativeW / Math.Max(0.0001, ItemAspectRatio);
        tentativeH = Math.Max(tentativeH, maxChildH);

        return (Math.Max(0, tentativeW), Math.Max(0, tentativeH));
    }

    private static int ComputeMaxColumns(double availableWidth, double cellWidth, double spacingX)
    {
        if (double.IsInfinity(availableWidth) || availableWidth <= 0 || cellWidth <= 0)
            return 1;

        // k*cell + (k-1)*spacing <= available  =>  k <= (available + spacing) / (cell + spacing)
        var denom = cellWidth + spacingX;
        if (denom <= 0) return 1;

        var k = (int)Math.Floor((availableWidth + spacingX) / denom);
        return Math.Max(1, k);
    }

    private static int ChooseColumnsMinimizingHoles(int count, int maxCols)
    {
        int bestCols = 1;
        int bestHoles = int.MaxValue;

        int upper = Math.Min(maxCols, Math.Max(1, count));
        for (int cols = 1; cols <= upper; cols++)
        {
            int rows = (int)Math.Ceiling(count / (double)cols);
            int holes = cols * rows - count;

            if (holes < bestHoles || (holes == bestHoles && cols > bestCols))
            {
                bestHoles = holes;
                bestCols = cols;
            }
        }

        return Math.Max(1, bestCols);
    }

    private static double ComputeCellWidth(double availableWidth, int columns, double spacingX, double preferredCellWidth, bool fillRow)
    {
        if (columns < 1)
            return Math.Max(0, preferredCellWidth);

        if (!fillRow || double.IsInfinity(availableWidth) || availableWidth <= 0)
            return Math.Max(0, preferredCellWidth);

        // Fill the row exactly
        var totalSpacing = (columns - 1) * spacingX;
        var widthForCells = Math.Max(0, availableWidth - totalSpacing);
        return widthForCells / columns;
    }

    private double ResolveHeight(double cellWidth, double preferredHeight)
    {
        if (ItemHeight is double fixedH)
            return fixedH;

        // Derive from aspect if height not fixed
        var hByAspect = cellWidth / Math.Max(0.0001, ItemAspectRatio);
        return Math.Max(preferredHeight, hByAspect);
    }
}

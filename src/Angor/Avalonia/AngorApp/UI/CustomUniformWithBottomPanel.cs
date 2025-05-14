
using Avalonia;

namespace AngorApp.UI;

public class CustomUniformWithBottomPanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<CustomUniformWithBottomPanel, int>(nameof(Columns), 3);

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Children.Count;
        if (count == 0)
            return new Size();

        int columns = Math.Max(1, Columns);
        int gridCount = Math.Max(0, count - 1);
        int rows = gridCount == 0 ? 0 : (int)Math.Ceiling(gridCount / (double)columns);

        double cellWidth = columns > 0 ? availableSize.Width / columns : 0;
        double cellHeight = 0;
        double bottomHeight = 0;

        for (int i = 0; i < gridCount; i++)
        {
            Children[i].Measure(new Size(cellWidth, double.PositiveInfinity));
            cellHeight = Math.Max(cellHeight, Children[i].DesiredSize.Height);
        }

        if (count > 0)
        {
            Children[count - 1].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            bottomHeight = Children[count - 1].DesiredSize.Height;
        }

        return new Size(
            availableSize.Width,
            (cellHeight * rows) + bottomHeight
        );
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = Children.Count;
        if (count == 0)
            return finalSize;

        int columns = Math.Max(1, Columns);
        int gridCount = Math.Max(0, count - 1);
        int rows = gridCount == 0 ? 0 : (int)Math.Ceiling(gridCount / (double)columns);

        double cellWidth = columns > 0 ? finalSize.Width / columns : 0;
        double cellHeight = 0;

        for (int i = 0; i < gridCount; i++)
        {
            cellHeight = Math.Max(cellHeight, Children[i].DesiredSize.Height);
        }

        for (int i = 0; i < gridCount; i++)
        {
            int row = i / columns;
            int col = i % columns;
            double x = col * cellWidth;
            double y = row * cellHeight;

            Children[i].Arrange(new Rect(x, y, cellWidth, cellHeight));
        }

        if (count > 0)
        {
            double y = (cellHeight * rows);
            Children[count - 1].Arrange(new Rect(0, y, finalSize.Width, Children[count - 1].DesiredSize.Height));
        }

        return finalSize;
    }
}
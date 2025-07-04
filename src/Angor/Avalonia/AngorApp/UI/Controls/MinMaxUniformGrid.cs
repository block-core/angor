using Avalonia;

namespace AngorApp.UI.Controls
{
    public class MinMaxUniformGrid : Panel
    {
        // Styled properties para los límites de ancho de columna
        public static readonly StyledProperty<double> MinColumnWidthProperty =
            AvaloniaProperty.Register<MinMaxUniformGrid, double>(
                nameof(MinColumnWidth), 
                0d);

        public static readonly StyledProperty<double> MaxColumnWidthProperty =
            AvaloniaProperty.Register<MinMaxUniformGrid, double>(
                nameof(MaxColumnWidth), 
                double.PositiveInfinity);

        static MinMaxUniformGrid()
        {
            // Indica que cambios en estas propiedades invalidan la medida
            AffectsMeasure<MinMaxUniformGrid>(MinColumnWidthProperty);
            AffectsMeasure<MinMaxUniformGrid>(MaxColumnWidthProperty);
        }

        public double MinColumnWidth
        {
            get => GetValue(MinColumnWidthProperty);
            set => SetValue(MinColumnWidthProperty, value);
        }

        public double MaxColumnWidth
        {
            get => GetValue(MaxColumnWidthProperty);
            set => SetValue(MaxColumnWidthProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // Ensure min ≤ max
            if (MinColumnWidth > MaxColumnWidth)
                throw new InvalidOperationException("MinColumnWidth debe ser ≤ MaxColumnWidth");

            int count = Children.Count;
            if (count == 0)
                return new Size();

            double width = availableSize.Width;
            // Determine number of columns based on limits
            int columns = count;
            if (!double.IsInfinity(width) && width > 0)
            {
                double cellWidth = width / count;
                if (cellWidth > MaxColumnWidth)
                    columns = (int)Math.Ceiling(width / MaxColumnWidth);
                else if (cellWidth < MinColumnWidth)
                    columns = (int)Math.Floor(width / MinColumnWidth);

                columns = Math.Min(Math.Max(columns, 1), count);
            }

            double actualCellWidth = double.IsInfinity(width) ? MaxColumnWidth : width / columns;
            double maxCellHeight = 0;

            foreach (var child in Children)
            {
                child.Measure(new Size(actualCellWidth, availableSize.Height));
                maxCellHeight = Math.Max(maxCellHeight, child.DesiredSize.Height);
            }

            int rows = (int)Math.Ceiling(count / (double)columns);
            return new Size(actualCellWidth * columns, maxCellHeight * rows);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = Children.Count;
            if (count == 0)
                return finalSize;

            double width = finalSize.Width;
            int columns = count;
            if (width > 0)
            {
                double cellWidth = width / count;
                if (cellWidth > MaxColumnWidth)
                    columns = (int)Math.Ceiling(width / MaxColumnWidth);
                else if (cellWidth < MinColumnWidth)
                    columns = (int)Math.Floor(width / MinColumnWidth);

                columns = Math.Min(Math.Max(columns, 1), count);
            }

            int rows = (int)Math.Ceiling(count / (double)columns);
            double cellWidthFinal = finalSize.Width / columns;
            double cellHeightFinal = finalSize.Height / rows;

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                // Arrange each child in its cell
                Children[i].Arrange(new Rect(
                    column * cellWidthFinal,
                    row * cellHeightFinal,
                    cellWidthFinal,
                    cellHeightFinal));
            }

            return finalSize;
        }
    }
}

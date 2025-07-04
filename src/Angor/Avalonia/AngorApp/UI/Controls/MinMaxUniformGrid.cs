using System;
using Avalonia;
using Avalonia.Controls;

namespace AngorApp.UI.Controls
{
    public class MinMaxUniformGrid : Panel
    {
        public static readonly StyledProperty<double> MinColumnWidthProperty =
            AvaloniaProperty.Register<MinMaxUniformGrid, double>(
                nameof(MinColumnWidth),
                0d);

        public static readonly StyledProperty<double> MaxColumnWidthProperty =
            AvaloniaProperty.Register<MinMaxUniformGrid, double>(
                nameof(MaxColumnWidth),
                double.PositiveInfinity);

        public static readonly StyledProperty<double> ColumnSpacingProperty =
            AvaloniaProperty.Register<MinMaxUniformGrid, double>(
                nameof(ColumnSpacing),
                0d);

        public static readonly StyledProperty<double> RowSpacingProperty =
            AvaloniaProperty.Register<MinMaxUniformGrid, double>(
                nameof(RowSpacing),
                0d);

        static MinMaxUniformGrid()
        {
            AffectsMeasure<MinMaxUniformGrid>(MinColumnWidthProperty);
            AffectsMeasure<MinMaxUniformGrid>(MaxColumnWidthProperty);
            AffectsMeasure<MinMaxUniformGrid>(ColumnSpacingProperty);
            AffectsMeasure<MinMaxUniformGrid>(RowSpacingProperty);
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

        protected override Size MeasureOverride(Size availableSize)
        {
            if (MinColumnWidth > MaxColumnWidth)
                throw new InvalidOperationException("MinColumnWidth must be ≤ MaxColumnWidth");

            int count = Children.Count;
            if (count == 0)
                return new Size();

            double totalWidth = availableSize.Width;
            int columns = count;

            if (!double.IsInfinity(totalWidth) && totalWidth > 0)
            {
                // Compute tentative columns based on width and limits
                double cellWidthGuess = (totalWidth - ColumnSpacing * (count - 1)) / count;
                if (cellWidthGuess > MaxColumnWidth)
                    columns = (int)Math.Ceiling((totalWidth + ColumnSpacing) / (MaxColumnWidth + ColumnSpacing));
                else if (cellWidthGuess < MinColumnWidth)
                    columns = (int)Math.Floor((totalWidth + ColumnSpacing) / (MinColumnWidth + ColumnSpacing));

                columns = Math.Min(Math.Max(columns, 1), count);
            }

            double availableCellWidth = double.IsInfinity(totalWidth)
                ? MaxColumnWidth
                : (totalWidth - ColumnSpacing * (columns - 1)) / columns;

            double maxCellHeight = 0;
            foreach (var child in Children)
            {
                child.Measure(new Size(availableCellWidth, availableSize.Height));
                maxCellHeight = Math.Max(maxCellHeight, child.DesiredSize.Height);
            }

            int rows = (int)Math.Ceiling(count / (double)columns);

            double totalHeight = maxCellHeight * rows + RowSpacing * (rows - 1);
            double measuredWidth = availableCellWidth * columns + ColumnSpacing * (columns - 1);

            return new Size(measuredWidth, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = Children.Count;
            if (count == 0)
                return finalSize;

            double totalWidth = finalSize.Width;
            int columns = count;

            if (totalWidth > 0)
            {
                double cellWidthGuess = (totalWidth - ColumnSpacing * (count - 1)) / count;
                if (cellWidthGuess > MaxColumnWidth)
                    columns = (int)Math.Ceiling((totalWidth + ColumnSpacing) / (MaxColumnWidth + ColumnSpacing));
                else if (cellWidthGuess < MinColumnWidth)
                    columns = (int)Math.Floor((totalWidth + ColumnSpacing) / (MinColumnWidth + ColumnSpacing));

                columns = Math.Min(Math.Max(columns, 1), count);
            }

            int rows = (int)Math.Ceiling(count / (double)columns);

            double cellWidthFinal = (finalSize.Width - ColumnSpacing * (columns - 1)) / columns;
            double cellHeightFinal = (finalSize.Height - RowSpacing * (rows - 1)) / rows;

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;

                double x = column * (cellWidthFinal + ColumnSpacing);
                double y = row * (cellHeightFinal + RowSpacing);

                // Arrange each child in its cell
                Children[i].Arrange(new Rect(x, y, cellWidthFinal, cellHeightFinal));
            }

            return finalSize;
        }
    }
}

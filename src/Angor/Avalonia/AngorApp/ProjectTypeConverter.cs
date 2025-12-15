using AngorApp.UI.Flows.CreateProject.Wizard;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AngorApp
{
    public static class ProjectTypeConverter
    {
        public static FuncValueConverter<ProjectType, IBrush> ToBrush { get; } =
            new FuncValueConverter<ProjectType, IBrush>(type =>
            {
                return type.Name switch
                {
                    "Investment" => Brushes.Green,
                    _ => Brushes.Orange,
                };
            });
    }
}
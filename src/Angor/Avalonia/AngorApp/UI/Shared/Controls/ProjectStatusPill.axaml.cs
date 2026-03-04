using AngorApp.Model.ProjectsV2.InvestmentProject;

namespace AngorApp.UI.Shared.Controls;

public partial class ProjectStatusPill : UserControl
{
    public ProjectStatusPill()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        UpdateVisibility();
    }

    public static bool ShouldShowStatusForProject(object? project)
    {
        return project is IInvestmentProject;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        IsVisible = ShouldShowStatusForProject(DataContext);
    }
}

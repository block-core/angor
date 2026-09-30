namespace App.UI.Sections.FindProjects;

/// <summary>
/// One virtualization row in the Find Projects grid — up to <see cref="ColumnCount"/>
/// project cards. The view renders rows through a VirtualizingStackPanel so only visible
/// rows are materialized; each row hosts its cards in a UniformRowPanel.
/// </summary>
public class ProjectRowViewModel(IReadOnlyList<ProjectItemViewModel> cards, int columnCount)
{
    public IReadOnlyList<ProjectItemViewModel> Cards { get; } = cards;
    public int ColumnCount { get; } = columnCount;
}

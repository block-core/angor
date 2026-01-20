using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AngorApp.UI.Sections.Founder;

public interface IFounderSectionViewModel
{
    ICommand LoadProjectsCommand { get; }
    ObservableCollection<IFounderProjectViewModel> ProjectsList { get; }
    ICommand CreateCommand { get; }
    bool IsLoading { get; }
    string? ErrorMessage { get; }
}

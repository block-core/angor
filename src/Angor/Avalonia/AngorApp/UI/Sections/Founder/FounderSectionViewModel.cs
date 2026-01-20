using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;
using Zafiro.UI.Shell.Utils;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;

namespace AngorApp.UI.Sections.Founder;

[Section("My Projects", icon: "fa-regular fa-file-lines", sortIndex: 4)]
[SectionGroup("FOUNDER")]
public class FounderSectionViewModel : IFounderSectionViewModel, INotifyPropertyChanged
{
    private readonly UIServices uiServices;
    private readonly IProjectAppService projectAppService;
    private readonly ICreateProjectFlow createProjectFlow;
    private readonly IWalletContext walletContext;
    private readonly Func<ProjectDto, IFounderProjectViewModel> projectViewModelFactory;
    
    private bool isLoading;
    private string? errorMessage;

    public FounderSectionViewModel(
        UIServices uiServices,
        IProjectAppService projectAppService,
        ICreateProjectFlow createProjectFlow,
        IWalletContext walletContext,
        Func<ProjectDto, IFounderProjectViewModel> projectViewModelFactory)
    {
        this.uiServices = uiServices;
        this.projectAppService = projectAppService;
        this.createProjectFlow = createProjectFlow;
        this.walletContext = walletContext;
        this.projectViewModelFactory = projectViewModelFactory;
        
        ProjectsList = new ObservableCollection<IFounderProjectViewModel>();
        LoadProjectsCommand = new AsyncCommand(LoadProjectsAsync, () => !IsLoading);
        CreateCommand = new AsyncCommand(CreateProjectAsync, () => !IsLoading);
    }

    public ObservableCollection<IFounderProjectViewModel> ProjectsList { get; }

    public ICommand LoadProjectsCommand { get; }

    public ICommand CreateCommand { get; }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetField(ref isLoading, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    private async Task LoadProjectsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        
        try
        {
            var walletResult = await walletContext.GetDefaultWallet();
            if (walletResult.IsFailure)
            {
                ErrorMessage = walletResult.Error;
                uiServices.NotificationService.Show("Failed to get investments", walletResult.Error);
                return;
            }

            var projectsResult = await projectAppService.GetFounderProjects(walletResult.Value.Id);
            if (projectsResult.IsFailure)
            {
                ErrorMessage = projectsResult.Error;
                uiServices.NotificationService.Show("Failed to get investments", projectsResult.Error);
                return;
            }

            ProjectsList.Clear();
            foreach (var projectDto in projectsResult.Value.Projects)
            {
                var viewModel = projectViewModelFactory(projectDto);
                ProjectsList.Add(viewModel);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            uiServices.NotificationService.Show("Failed to get investments", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CreateProjectAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        
        try
        {
            var result = await createProjectFlow.CreateProject();
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                uiServices.NotificationService.Show("Cannot create project", result.Error);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            uiServices.NotificationService.Show("Cannot create project", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private class AsyncCommand : ICommand
    {
        private readonly Func<Task> execute;
        private readonly Func<bool> canExecute;
        private bool isExecuting;

        public AsyncCommand(Func<Task> execute, Func<bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !isExecuting && canExecute();

        public async void Execute(object? parameter)
        {
            if (isExecuting) return;
            
            isExecuting = true;
            RaiseCanExecuteChanged();
            
            try
            {
                await execute();
            }
            finally
            {
                isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

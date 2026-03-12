using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia2.UI.Sections.MyProjects.Deploy;
using Avalonia2.UI.Sections.MyProjects.Steps;
using Avalonia2.UI.Shell;
using ReactiveUI;

namespace Avalonia2.UI.Sections.MyProjects;

public partial class CreateProjectView : UserControl
{
    // Stepper elements — resolved by name in AXAML
    private Border[] _stepCircles = [];
    private TextBlock[] _stepNums = [];
    private TextBlock[] _stepLabels = [];
    private Border[] _stepLines = [];
    private Button[] _stepButtons = [];

    private IDisposable? _deploySubscription;
    private IDisposable? _stepSubscription;
    private IDisposable? _typeSubscription;
    private CancellationTokenSource? _autoSavedCts;

    public CreateProjectView()
    {
        InitializeComponent();
        DataContext = new CreateProjectViewModel();

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Update stepper visuals whenever the current step changes
        if (DataContext is CreateProjectViewModel vm)
        {
            _stepSubscription = vm.WhenAnyValue(x => x.CurrentStep)
                .Subscribe(_ => UpdateStepper());

            // Update stepper labels when project type changes (step names change)
            _typeSubscription = vm.WhenAnyValue(x => x.ProjectType)
                .Subscribe(_ => UpdateStepperLabels());

            // Watch deploy flow visibility to push modal to shell
            _deploySubscription = vm.DeployFlow.WhenAnyValue(x => x.IsVisible)
                .Subscribe(isVisible =>
                {
                    if (isVisible)
                        ShowDeployShellModal(vm);
                });
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResolveNamedElements();
        UpdateStepper();
    }

    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // Re-subscribe after view is re-attached from cache (subscriptions are
        // disposed in OnDetachedFromLogicalTree when the user navigates away).
        if (_deploySubscription == null && DataContext is CreateProjectViewModel vm)
        {
            _stepSubscription = vm.WhenAnyValue(x => x.CurrentStep)
                .Subscribe(_ => UpdateStepper());

            _typeSubscription = vm.WhenAnyValue(x => x.ProjectType)
                .Subscribe(_ => UpdateStepperLabels());

            _deploySubscription = vm.DeployFlow.WhenAnyValue(x => x.IsVisible)
                .Subscribe(isVisible =>
                {
                    if (isVisible)
                        ShowDeployShellModal(vm);
                });
        }
    }

    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _deploySubscription?.Dispose();
        _deploySubscription = null;
        _stepSubscription?.Dispose();
        _stepSubscription = null;
        _typeSubscription?.Dispose();
        _typeSubscription = null;
    }

    private void ResolveNamedElements()
    {
        _stepCircles =
        [
            this.FindControl<Border>("StepCircle1")!,
            this.FindControl<Border>("StepCircle2")!,
            this.FindControl<Border>("StepCircle3")!,
            this.FindControl<Border>("StepCircle4")!,
            this.FindControl<Border>("StepCircle5")!,
            this.FindControl<Border>("StepCircle6")!,
        ];
        _stepNums =
        [
            this.FindControl<TextBlock>("StepNum1")!,
            this.FindControl<TextBlock>("StepNum2")!,
            this.FindControl<TextBlock>("StepNum3")!,
            this.FindControl<TextBlock>("StepNum4")!,
            this.FindControl<TextBlock>("StepNum5")!,
            this.FindControl<TextBlock>("StepNum6")!,
        ];
        _stepLabels =
        [
            this.FindControl<TextBlock>("StepLabel1")!,
            this.FindControl<TextBlock>("StepLabel2")!,
            this.FindControl<TextBlock>("StepLabel3")!,
            this.FindControl<TextBlock>("StepLabel4")!,
            this.FindControl<TextBlock>("StepLabel5")!,
            this.FindControl<TextBlock>("StepLabel6")!,
        ];
        _stepLines =
        [
            this.FindControl<Border>("StepLine1")!,
            this.FindControl<Border>("StepLine2")!,
            this.FindControl<Border>("StepLine3")!,
            this.FindControl<Border>("StepLine4")!,
            this.FindControl<Border>("StepLine5")!,
        ];
        _stepButtons =
        [
            this.FindControl<Button>("StepBtn1")!,
            this.FindControl<Button>("StepBtn2")!,
            this.FindControl<Button>("StepBtn3")!,
            this.FindControl<Button>("StepBtn4")!,
            this.FindControl<Button>("StepBtn5")!,
            this.FindControl<Button>("StepBtn6")!,
        ];
    }

    private CreateProjectViewModel? Vm => DataContext as CreateProjectViewModel;

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "StartButton":
                Vm?.DismissWelcome();
                UpdateStepper();
                break;
            case "Step5WelcomeButton":
                Vm?.DismissStep5Welcome();
                break;
            case "NextStepButton":
                var stepBefore = Vm?.CurrentStep;
                Vm?.GoNext();
                // Vue: show "Auto saved" indicator for 5s when step advances past step 2
                if (Vm != null && Vm.CurrentStep != stepBefore && stepBefore >= 2)
                    ShowAutoSaved();
                break;
            case "PrevStepButton":
                if (Vm?.CurrentStep == 1)
                    NavigateBackToMyProjects();
                else
                    Vm?.GoBack();
                break;
            case "DeployButton":
                Vm?.Deploy();
                break;
            // Note: UploadBannerButton and UploadAvatarButton are handled directly by Step3
            // Step 5 buttons — events bubble up from child UC
            case "GenerateStagesButton": Vm?.GenerateInvestmentStages(); break;
            case "GeneratePayoutsButton": Vm?.GeneratePayoutSchedule(); break;
            case "DeleteStagesButton": Vm?.ClearStages(); break;
            case "ToggleEditorButton": Vm?.ToggleAdvancedEditor(); break;
            case "RegenerateStagesButton": Vm?.ShowRegenerateForm(); break;
            case "RegeneratePayoutsButton": Vm?.ShowRegenerateForm(); break;
            // Stepper step buttons
            case "StepBtn1": Vm?.GoToStep(1); break;
            case "StepBtn2": Vm?.GoToStep(2); break;
            case "StepBtn3": Vm?.GoToStep(3); break;
            case "StepBtn4": Vm?.GoToStep(4); break;
            case "StepBtn5": Vm?.GoToStep(5); break;
            case "StepBtn6": Vm?.GoToStep(6); break;
        }
    }

    #region Stepper

    /// <summary>
    /// Update the vertical stepper via CSS class toggling.
    /// Completed: StepCompleted class. Current: StepCurrent class. Future: no modifier class.
    /// </summary>
    private void UpdateStepper()
    {
        if (Vm == null || _stepCircles.Length == 0) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            for (int i = 0; i < 6; i++)
            {
                var stepNum = i + 1;
                var isCompleted = stepNum < Vm.CurrentStep;
                var isCurrent = stepNum == Vm.CurrentStep;

                // Circle
                _stepCircles[i].Classes.Set("StepCompleted", isCompleted);
                _stepCircles[i].Classes.Set("StepCurrent", isCurrent);

                // Number text
                _stepNums[i].Classes.Set("StepCompleted", isCompleted);
                _stepNums[i].Classes.Set("StepCurrent", isCurrent);
                _stepNums[i].Text = isCompleted ? "\u2713" : stepNum.ToString();

                // Label
                _stepLabels[i].Classes.Set("StepCompleted", isCompleted);
                _stepLabels[i].Classes.Set("StepCurrent", isCurrent);
            }

            // Connecting lines — green when the step above is completed
            for (int i = 0; i < 5; i++)
            {
                _stepLines[i].Classes.Set("StepLineCompleted", i + 1 < Vm.CurrentStep);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Update stepper labels when project type changes (step 4/5 names vary).
    /// </summary>
    private void UpdateStepperLabels()
    {
        if (Vm == null || _stepLabels.Length == 0) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var names = Vm.StepNames;
            for (int i = 0; i < Math.Min(names.Length, _stepLabels.Length); i++)
            {
                _stepLabels[i].Text = names[i];
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    #endregion

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    /// <summary>
    /// Create DeployFlowOverlay and push it to the shell-level modal overlay.
    /// Same pattern as InvestPageView.ShowShellModal().
    /// </summary>
    private void ShowDeployShellModal(CreateProjectViewModel vm)
    {
        var shellVm = GetShellVm();
        if (shellVm == null || shellVm.IsModalOpen) return;

        var overlay = new DeployFlowOverlay
        {
            DataContext = vm.DeployFlow
        };

        shellVm.ShowModal(overlay);
    }

    /// <summary>
    /// Reset all visual state so the wizard appears fresh.
    /// Delegates to child step UserControls that have their own ResetVisualState().
    /// Called by MyProjectsView.OpenCreateWizard() after VM.ResetWizard().
    /// </summary>
    public void ResetVisualState()
    {
        // Reset stepper
        UpdateStepper();

        // Delegate to child step UCs
        this.FindControl<CreateProjectStep1View>("Step1View")?.ResetVisualState();
        this.FindControl<CreateProjectStep3View>("Step3View")?.ResetVisualState();
        this.FindControl<CreateProjectStep4View>("Step4View")?.ResetVisualState();
        this.FindControl<CreateProjectStep5View>("Step5View")?.ResetVisualState();
    }

    /// <summary>
    /// Show the "Auto saved" indicator in the top-right of the content area.
    /// Vue: showAutoSaved = true, setTimeout 5000ms to hide.
    /// Semi-transparent, fade in/out 0.3s via Opacity transition.
    /// </summary>
    private async void ShowAutoSaved()
    {
        var indicator = this.FindControl<Border>("AutoSavedIndicator");
        if (indicator == null) return;

        // Cancel any previous dismiss timer
        _autoSavedCts?.Cancel();
        _autoSavedCts = new CancellationTokenSource();
        var token = _autoSavedCts.Token;

        // Show + fade in
        indicator.IsVisible = true;
        indicator.Opacity = 0.9;

        try
        {
            // Vue: auto-dismiss after 5s
            await Task.Delay(5000, token);
            if (token.IsCancellationRequested) return;

            // Fade out
            indicator.Opacity = 0;
            await Task.Delay(300, token); // wait for opacity transition
            if (!token.IsCancellationRequested)
                indicator.IsVisible = false;
        }
        catch (TaskCanceledException) { /* new ShowAutoSaved() call replaced this one */ }
    }

    private void NavigateBackToMyProjects()
    {
        var myProjectsView = this.FindAncestorOfType<MyProjectsView>();
        if (myProjectsView?.DataContext is MyProjectsViewModel myVm)
        {
            myVm.ShowCreateWizard = false;
        }
    }
}

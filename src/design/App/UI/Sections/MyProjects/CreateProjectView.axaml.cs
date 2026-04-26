using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Sections.MyProjects.Steps;
using App.UI.Shared;
using App.UI.Shared.PaymentFlow;
using App.UI.Shell;
using ReactiveUI;

namespace App.UI.Sections.MyProjects;

public partial class CreateProjectView : UserControl
{
    // Stepper elements — resolved by name in AXAML
    private Border[] _stepCircles = [];
    private TextBlock[] _stepNums = [];
    private TextBlock[] _stepLabels = [];
    private Border[] _stepLines = [];
    private Button[] _stepButtons = [];

    // Responsive layout — cached controls
    private Grid? _wizardMainGrid;
    private Grid? _stepperColumn;
    private Border? _mobileWizardHeader;
    private TextBlock? _mobileStepTitle;
    private Border[] _progressSegments = [];
    private Border? _navFooter;
    private StackPanel? _stepContentPanel;

    private IDisposable? _deploySubscription;
    private IDisposable? _stepSubscription;
    private IDisposable? _typeSubscription;
    private IDisposable? _layoutSubscription;
    private CancellationTokenSource? _autoSavedCts;

    // Track current compact state for UpdateMobileHeader
    private bool _isCompact;

    public CreateProjectView()
    {
        InitializeComponent();
        // DataContext is set by the parent MyProjectsView (XAML-embedded view)

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Cache responsive controls
        _wizardMainGrid = this.FindControl<Grid>("WizardMainGrid");
        _stepperColumn = this.FindControl<Grid>("StepperColumn");
        _mobileWizardHeader = this.FindControl<Border>("MobileWizardHeader");
        _mobileStepTitle = this.FindControl<TextBlock>("MobileStepTitle");
        _navFooter = this.FindControl<Border>("NavFooter");
        _stepContentPanel = this.FindControl<StackPanel>("StepContentPanel");

        // Cache progress bar segments
        _progressSegments =
        [
            this.FindControl<Border>("ProgressSeg1")!,
            this.FindControl<Border>("ProgressSeg2")!,
            this.FindControl<Border>("ProgressSeg3")!,
            this.FindControl<Border>("ProgressSeg4")!,
            this.FindControl<Border>("ProgressSeg5")!,
            this.FindControl<Border>("ProgressSeg6")!,
        ];

        // Subscribe to layout mode changes
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);

        DataContextChanged += OnDataContextSet;
    }

    private void OnDataContextSet(object? sender, EventArgs e)
    {
        // Subscribe to VM observables when DataContext is set by the parent
        SubscribeToVm();
    }

    private void SubscribeToVm()
    {
        _stepSubscription?.Dispose();
        _typeSubscription?.Dispose();
        _deploySubscription?.Dispose();

        if (DataContext is CreateProjectViewModel vm)
        {
            _stepSubscription = vm.WhenAnyValue(x => x.CurrentStep)
                .Subscribe(_ =>
                {
                    UpdateStepper();
                    UpdateMobileHeader();
                });

            _typeSubscription = vm.WhenAnyValue(x => x.ProjectType)
                .Subscribe(_ =>
                {
                    UpdateStepperLabels();
                    UpdateMobileHeader();
                });

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
        UpdateMobileHeader();
    }

    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // Re-subscribe after view is re-attached from cache (subscriptions are
        // disposed in OnDetachedFromLogicalTree when the user navigates away).
        if (_deploySubscription == null)
            SubscribeToVm();

        // Re-subscribe layout if needed
        _layoutSubscription ??= LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
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
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
    }

    #region Responsive Layout

    /// <summary>
    /// Responsive layout: compact → hide stepper sidebar, show mobile header with progress bar.
    /// Vue: App.vue lines 585-650 — completely different template branch on mobile.
    /// Desktop (>=1024px): two-column — left stepper sidebar (250px) + right content.
    /// Mobile (<1024px): no stepper sidebar, mobile header with step title + close + progress bar.
    ///
    /// IMPORTANT: XAML pre-declares <c>ColumnDefinitions="250,*"</c>. Mutate the
    /// existing <c>GridLength</c> values only — never <c>Clear()</c>+<c>Add()</c>
    /// the collection. Replacing it causes SIGABRT on macOS because children
    /// briefly reference invalid column indices during the swap. Pattern
    /// established in c19cd35f.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        _isCompact = isCompact;

        if (_wizardMainGrid == null) return;

        var cols = _wizardMainGrid.ColumnDefinitions;

        if (isCompact)
        {
            // Hide stepper sidebar column
            if (_stepperColumn != null) _stepperColumn.IsVisible = false;

            // Collapse the stepper column width to 0
            if (cols.Count >= 2)
            {
                cols[0].Width = new GridLength(0);
                cols[1].Width = GridLength.Star;
            }

            // Show mobile header
            if (_mobileWizardHeader != null) _mobileWizardHeader.IsVisible = true;

            // Add top margin to main grid to clear the mobile header
            // Mobile header height: ~16 (padding) + 22 (title) + 12 (spacing) + 6 (progress) + 16 (padding bottom) ≈ 72
            _wizardMainGrid.Margin = new Thickness(0, 72, 0, 0);

            // Reduce content panel side margins for compact screens
            // Vue: mobile content uses p-4 (16px) instead of 32px
            if (_stepContentPanel != null)
                _stepContentPanel.Margin = new Thickness(16, 16, 16, 120); // 120px bottom for tab bar clearance

            // Nav footer: flush with tab bar — no bottom margin needed since
            // the footer is docked to the bottom of Row 1, directly above Row 2 (tab bar)
            if (_navFooter != null)
                _navFooter.Margin = new Thickness(0);
        }
        else
        {
            // Show stepper sidebar column
            if (_stepperColumn != null) _stepperColumn.IsVisible = true;

            // Restore two-column layout
            if (cols.Count >= 2)
            {
                cols[0].Width = new GridLength(250);
                cols[1].Width = GridLength.Star;
            }

            // Hide mobile header
            if (_mobileWizardHeader != null) _mobileWizardHeader.IsVisible = false;

            // Restore margins
            _wizardMainGrid.Margin = new Thickness(0);

            if (_stepContentPanel != null)
                _stepContentPanel.Margin = new Thickness(32, 32, 32, 24);

            if (_navFooter != null)
                _navFooter.Margin = new Thickness(0);
        }

        UpdateMobileHeader();
    }

    /// <summary>
    /// Update the mobile header step title and progress bar segments.
    /// Vue: getStepTitle(currentStep) for title text.
    /// Progress bar: green gradient for steps <= currentStep, gray for future.
    /// </summary>
    private void UpdateMobileHeader()
    {
        if (!_isCompact || Vm == null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Update step title
            if (_mobileStepTitle != null)
            {
                var names = Vm.StepNames;
                var stepIdx = Vm.CurrentStep - 1;
                if (stepIdx >= 0 && stepIdx < names.Length)
                    _mobileStepTitle.Text = names[stepIdx];
            }

            // Update progress segments
            // Vue: step <= currentStep → green gradient, else gray-200
            var greenGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#2D5A3D"), 0),
                    new GradientStop(Color.Parse("#4D7A5D"), 1),
                }
            };

            for (int i = 0; i < _progressSegments.Length; i++)
            {
                var seg = _progressSegments[i];
                if (seg == null) continue;

                if (i + 1 <= Vm.CurrentStep)
                {
                    seg.Background = greenGradient;
                }
                else
                {
                    // Use DynamicResource StrokeSubtle — find from resources
                    if (this.TryFindResource("StrokeSubtle", this.ActualThemeVariant, out var res) && res is IBrush brush)
                        seg.Background = brush;
                    else
                        seg.Background = new SolidColorBrush(Color.Parse("#E5E7EB"));
                }
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    #endregion

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
                UpdateMobileHeader();
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
            // Mobile close button — same as cancel/back to my projects
            case "MobileCloseBtn":
                NavigateBackToMyProjects();
                break;
            // Note: UploadBannerButton and UploadAvatarButton are handled directly by Step3
            // Step 5 buttons — events bubble up from child UC
            case "GenerateStagesButton": Vm?.GenerateInvestmentStages(); break;
            case "GeneratePayoutsButton": Vm?.GeneratePayoutSchedule(); break;
            case "DeleteStagesButton": Vm?.ClearStages(); break;
            case "ToggleEditorButton": Vm?.ToggleAdvancedEditor(); break;
            case "AddStageButton": Vm?.AddStage(); break;
            case "RemoveStageButton":
                if (btn.Tag is ProjectStageViewModel stageToRemove)
                    Vm?.RemoveStage(stageToRemove);
                break;
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
    /// Create PaymentFlowView backed by the deploy flow's PaymentFlowViewModel
    /// and push it to the shell-level modal overlay.
    /// </summary>
    private void ShowDeployShellModal(CreateProjectViewModel vm)
    {
        var shellVm = GetShellVm();
        if (shellVm == null || shellVm.IsModalOpen) return;

        var paymentFlow = vm.DeployFlow.PaymentFlow;
        if (paymentFlow == null) return;

        var paymentFlowView = new PaymentFlowView { DataContext = paymentFlow };
        shellVm.ShowModal(paymentFlowView);
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
        UpdateMobileHeader();

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

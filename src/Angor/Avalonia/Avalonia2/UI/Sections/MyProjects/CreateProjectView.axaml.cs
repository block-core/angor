using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia2.UI.Sections.MyProjects.Deploy;
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

    // Type card elements — each card has: border, radio check indicator
    private Border[] _typeCards = [];
    private Viewbox[] _radioChecks = [];
    private string? _selectedType;

    // ListBox preset controls — Step 4
    private ListBox? _investAmountPresets;
    private ListBox? _fundAmountPresets;
    private ListBox? _subPricePresets;
    private ListBox? _durationPresets;

    // ListBox controls — Step 5 (Investment)
    private ListBox? _investFrequencyPresets;

    // ListBox controls — Step 5 (Fund/Subscription)
    private Border? _payoutFreqMonthly;
    private Border? _payoutFreqWeekly;
    private Border? _radioOuterMonthly;
    private Border? _radioOuterWeekly;
    private Ellipse? _radioDotMonthly;
    private Ellipse? _radioDotWeekly;
    private TextBlock? _payoutFreqMonthlyText;
    private TextBlock? _payoutFreqWeeklyText;
    private ListBox? _monthlyDateGrid;
    private ListBox? _weeklyDayList;

    // Installment multiselect borders
    private Border? _installment3;
    private Border? _installment6;
    private Border? _installment9;
    private Border? _check3;
    private Border? _check6;
    private Border? _check9;
    private Control? _checkIcon3;
    private Control? _checkIcon6;
    private Control? _checkIcon9;
    private TextBlock? _installmentText3;
    private TextBlock? _installmentText6;
    private TextBlock? _installmentText9;

    private IDisposable? _deploySubscription;
    private IDisposable? _stepSubscription;
    private IDisposable? _typeSubscription;
    private IDisposable? _durationValueSubscription;

    // Track selected duration preset button for CSS class toggling
    private Button? _selectedDurationPresetBtn;

    public CreateProjectView()
    {
        InitializeComponent();
        DataContext = new CreateProjectViewModel();

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Wire avatar border click (it's a Border, not a Button, so Button.ClickEvent won't fire)
        var avatarBorder = this.FindControl<Border>("UploadAvatarButton");
        if (avatarBorder != null)
            avatarBorder.PointerPressed += (_, _) => _ = PickImageAsync(false);

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

            // Clear duration preset selection when user manually types a non-matching value,
            // or when the duration unit changes (items get regenerated)
            _durationValueSubscription = vm.WhenAnyValue(x => x.DurationValue, x => x.DurationUnit)
                .Subscribe(tuple =>
                {
                    var (val, _) = tuple;
                    if (_selectedDurationPresetBtn?.Tag is DurationPresetItem preset
                        && val != preset.Value.ToString())
                    {
                        _selectedDurationPresetBtn.Classes.Set("DurPresetSelected", false);
                        _selectedDurationPresetBtn = null;
                    }
                    else if (_selectedDurationPresetBtn != null
                             && _selectedDurationPresetBtn.Tag is not DurationPresetItem)
                    {
                        // Button was recycled / items regenerated — stale reference
                        _selectedDurationPresetBtn = null;
                    }
                });
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResolveNamedElements();
        UpdateStepper();
        // Apply initial unselected styles to all type cards so they don't flash
        // with XAML defaults before user interaction
        ApplyTypeCardStyles();
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

            _durationValueSubscription = vm.WhenAnyValue(x => x.DurationValue, x => x.DurationUnit)
                .Subscribe(tuple =>
                {
                    var (val, _) = tuple;
                    if (_selectedDurationPresetBtn?.Tag is DurationPresetItem preset
                        && val != preset.Value.ToString())
                    {
                        _selectedDurationPresetBtn.Classes.Set("DurPresetSelected", false);
                        _selectedDurationPresetBtn = null;
                    }
                    else if (_selectedDurationPresetBtn != null
                             && _selectedDurationPresetBtn.Tag is not DurationPresetItem)
                    {
                        _selectedDurationPresetBtn = null;
                    }
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
        _durationValueSubscription?.Dispose();
        _durationValueSubscription = null;
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

        // Type card elements — now Border (not Button) to prevent hover chrome
        _typeCards =
        [
            this.FindControl<Border>("TypeInvestCard")!,
            this.FindControl<Border>("TypeFundCard")!,
            this.FindControl<Border>("TypeSubscriptionCard")!,
        ];
        _radioChecks =
        [
            this.FindControl<Viewbox>("RadioCheckInvest")!,
            this.FindControl<Viewbox>("RadioCheckFund")!,
            this.FindControl<Viewbox>("RadioCheckSub")!,
        ];

        // Wire up PointerPressed on type cards (Border, not Button)
        var typeNames = new[] { "investment", "fund", "subscription" };
        for (int i = 0; i < _typeCards.Length; i++)
        {
            var typeName = typeNames[i];
            _typeCards[i].PointerPressed += (_, _) =>
            {
                Vm?.SelectProjectType(typeName);
                HighlightTypeCard(typeName);
            };
        }

        // Hover effects are handled by :pointerover styles in XAML

        // Resolve ListBox preset controls — Step 4
        _investAmountPresets = this.FindControl<ListBox>("InvestAmountPresets");
        _fundAmountPresets = this.FindControl<ListBox>("FundAmountPresets");
        _subPricePresets = this.FindControl<ListBox>("SubPricePresets");
        _durationPresets = this.FindControl<ListBox>("DurationPresets");

        // Resolve ListBox controls — Step 5 (Investment)
        _investFrequencyPresets = this.FindControl<ListBox>("InvestFrequencyPresets");

        // Resolve payout frequency manual borders — Step 5 (Fund/Subscription)
        _payoutFreqMonthly = this.FindControl<Border>("PayoutFreqMonthly");
        _payoutFreqWeekly = this.FindControl<Border>("PayoutFreqWeekly");
        _radioOuterMonthly = this.FindControl<Border>("RadioOuterMonthly");
        _radioOuterWeekly = this.FindControl<Border>("RadioOuterWeekly");
        _radioDotMonthly = this.FindControl<Ellipse>("RadioDotMonthly");
        _radioDotWeekly = this.FindControl<Ellipse>("RadioDotWeekly");
        _payoutFreqMonthlyText = this.FindControl<TextBlock>("PayoutFreqMonthlyText");
        _payoutFreqWeeklyText = this.FindControl<TextBlock>("PayoutFreqWeeklyText");
        _monthlyDateGrid = this.FindControl<ListBox>("MonthlyDateGrid");
        _weeklyDayList = this.FindControl<ListBox>("WeeklyDayList");

        // Resolve installment multiselect borders
        _installment3 = this.FindControl<Border>("Installment3");
        _installment6 = this.FindControl<Border>("Installment6");
        _installment9 = this.FindControl<Border>("Installment9");
        _check3 = this.FindControl<Border>("Check3");
        _check6 = this.FindControl<Border>("Check6");
        _check9 = this.FindControl<Border>("Check9");
        _checkIcon3 = this.FindControl<Control>("CheckIcon3");
        _checkIcon6 = this.FindControl<Control>("CheckIcon6");
        _checkIcon9 = this.FindControl<Control>("CheckIcon9");
        _installmentText3 = this.FindControl<TextBlock>("InstallmentText3");
        _installmentText6 = this.FindControl<TextBlock>("InstallmentText6");
        _installmentText9 = this.FindControl<TextBlock>("InstallmentText9");

        // Wire up Step 4 ListBox selection changed handlers
        if (_investAmountPresets != null)
            _investAmountPresets.SelectionChanged += (_, _) => OnAmountPresetSelected(_investAmountPresets);
        if (_fundAmountPresets != null)
            _fundAmountPresets.SelectionChanged += (_, _) => OnAmountPresetSelected(_fundAmountPresets);
        if (_subPricePresets != null)
            _subPricePresets.SelectionChanged += (_, _) => OnSubPricePresetSelected(_subPricePresets);
        if (_durationPresets != null)
            _durationPresets.SelectionChanged += (_, _) => OnDurationPresetSelected(_durationPresets);

        // Wire up Step 5 ListBox selection changed handlers (Investment)
        if (_investFrequencyPresets != null)
            _investFrequencyPresets.SelectionChanged += (_, _) => OnInvestFrequencySelected();

        // Wire up Step 5 payout frequency click handlers (Fund/Subscription)
        WirePayoutFreqBorder(_payoutFreqMonthly, "Monthly");
        WirePayoutFreqBorder(_payoutFreqWeekly, "Weekly");
        if (_monthlyDateGrid != null)
            _monthlyDateGrid.SelectionChanged += (_, _) => OnMonthlyDateSelected();
        if (_weeklyDayList != null)
            _weeklyDayList.SelectionChanged += (_, _) => OnWeeklyDaySelected();

        // Wire up installment multiselect click handlers
        WireInstallmentBorder(_installment3, 3);
        WireInstallmentBorder(_installment6, 6);
        WireInstallmentBorder(_installment9, 9);
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
                Vm?.GoNext();
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
            // Image upload buttons
            case "UploadBannerButton": _ = PickImageAsync(true); break;
            // Note: UploadAvatarButton is a Border (not Button) — handled via PointerPressed in constructor
            // Step 5 buttons
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

    #region Type Cards

    /// <summary>
    /// Set the selected type card via CSS class toggling.
    /// Styles handle all visual states — code-behind only toggles TypeCardSelected class
    /// and shows/hides radio check indicators.
    /// </summary>
    private void HighlightTypeCard(string type)
    {
        _selectedType = type;
        ApplyTypeCardStyles();
    }

    /// <summary>
    /// Apply visual states to all three type cards via CSS class toggling.
    /// Zero FindResource, zero ClearValue, zero SolidColorBrush construction.
    /// </summary>
    private void ApplyTypeCardStyles()
    {
        if (_typeCards.Length == 0) return;

        var types = new[] { "investment", "fund", "subscription" };

        for (int i = 0; i < 3; i++)
        {
            var isSelected = types[i] == _selectedType;

            // Toggle selected class on the card border — styles handle everything
            _typeCards[i].Classes.Set("TypeCardSelected", isSelected);

            // Show/hide pre-built radio check indicator
            _radioChecks[i].IsVisible = isSelected;
        }
    }

    #endregion

    #region ListBox Preset Handlers

    /// <summary>
    /// Handle amount preset selection (Investment target amount or Fund goal).
    /// Reads the Tag from the selected ListBoxItem and sets TargetAmount on the VM.
    /// </summary>
    private void OnAmountPresetSelected(ListBox lb)
    {
        if (lb.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.TargetAmount = tag;
        }
    }

    /// <summary>
    /// Handle subscription price preset selection.
    /// Reads the Tag from the selected ListBoxItem and sets SubscriptionPrice on the VM.
    /// </summary>
    private void OnSubPricePresetSelected(ListBox lb)
    {
        if (lb.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.SubscriptionPrice = tag;
        }
    }

    /// <summary>
    /// Handle duration preset selection (Investment end date).
    /// Reads the Tag (months as string) and sets the end date relative to now.
    /// </summary>
    private void OnDurationPresetSelected(ListBox lb)
    {
        if (lb.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && int.TryParse(tag, out var months) && Vm != null)
        {
            Vm.InvestEndDate = DateTime.Now.AddMonths(months);
        }
    }

    /// <summary>
    /// Handle dynamic duration preset button click (Step 5 Investment).
    /// The button's Tag contains the preset value (int), DataContext is the bound int from DurationPresetItems.
    /// Toggles DurPresetSelected CSS class on the clicked button.
    /// </summary>
    private void OnDurationPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DurationPresetItem preset && Vm != null)
        {
            // Deselect previous
            _selectedDurationPresetBtn?.Classes.Set("DurPresetSelected", false);

            // Select new
            _selectedDurationPresetBtn = btn;
            btn.Classes.Set("DurPresetSelected", true);

            Vm.DurationPreset = preset.Value;
        }
    }

    /// <summary>
    /// Handle Investment release frequency selection on Step 5.
    /// Sets ReleaseFrequency on the VM.
    /// </summary>
    private void OnInvestFrequencySelected()
    {
        if (_investFrequencyPresets?.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.ReleaseFrequency = tag;
        }
    }

    /// <summary>
    /// Handle Fund/Sub payout frequency selection on Step 5.
    /// Sets PayoutFrequency ("Monthly" or "Weekly") on the VM.
    /// Uses manual Border elements with radio indicator (like installments pattern).
    /// </summary>
    private void WirePayoutFreqBorder(Border? border, string frequency)
    {
        if (border == null) return;
        border.PointerPressed += (_, _) =>
        {
            if (Vm != null)
            {
                Vm.PayoutFrequency = frequency;
                UpdatePayoutFreqVisuals();
            }
        };
    }

    /// <summary>
    /// Update payout frequency radio visuals based on VM.PayoutFrequency.
    /// Vue: 20x20 rounded-full border-2, with 12x12 green filled circle inside when active.
    /// </summary>
    private void UpdatePayoutFreqVisuals()
    {
        if (Vm == null) return;
        var freq = Vm.PayoutFrequency;
        var isMonthly = freq == "Monthly";
        var isWeekly = freq == "Weekly";

        // Row border + bg
        _payoutFreqMonthly?.Classes.Set("PayoutFreqSelected", isMonthly);
        _payoutFreqWeekly?.Classes.Set("PayoutFreqSelected", isWeekly);

        // Radio outer ring
        _radioOuterMonthly?.Classes.Set("RadioSelected", isMonthly);
        _radioOuterWeekly?.Classes.Set("RadioSelected", isWeekly);

        // Radio inner dot visibility
        if (_radioDotMonthly != null) _radioDotMonthly.IsVisible = isMonthly;
        if (_radioDotWeekly != null) _radioDotWeekly.IsVisible = isWeekly;

        // Text color: green when selected
        _payoutFreqMonthlyText?.Classes.Set("PayoutFreqTextSelected", isMonthly);
        _payoutFreqWeeklyText?.Classes.Set("PayoutFreqTextSelected", isWeekly);
    }

    /// <summary>
    /// Wire a single installment border for multiselect click toggling.
    /// Vue: toggleInstallmentCount() — toggle count in/out of installmentCounts array.
    /// </summary>
    private void WireInstallmentBorder(Border? border, int count)
    {
        if (border == null) return;
        border.PointerPressed += (_, _) =>
        {
            Vm?.ToggleInstallmentCount(count);
            UpdateInstallmentVisuals();
        };
    }

    /// <summary>
    /// Update installment checkbox visuals based on SelectedInstallmentCounts.
    /// Vue: .settings-toggle-button.active → green bg + white checkmark.
    /// </summary>
    private void UpdateInstallmentVisuals()
    {
        if (Vm == null) return;
        UpdateSingleInstallment(3, _installment3, _check3, _checkIcon3, _installmentText3);
        UpdateSingleInstallment(6, _installment6, _check6, _checkIcon6, _installmentText6);
        UpdateSingleInstallment(9, _installment9, _check9, _checkIcon9, _installmentText9);
    }

    private void UpdateSingleInstallment(int count, Border? row, Border? checkBorder, Control? checkIcon, TextBlock? text)
    {
        if (Vm == null) return;
        var isSelected = Vm.SelectedInstallmentCounts.Contains(count);

        // Row border + bg
        row?.Classes.Set("InstallmentSelected", isSelected);

        // Checkbox: green bg + green border when selected, transparent otherwise
        if (checkBorder != null)
        {
            checkBorder.Classes.Set("CheckboxActive", isSelected);
        }

        // Checkmark icon visibility
        if (checkIcon != null)
            checkIcon.IsVisible = isSelected;

        // Text color: green when selected (Vue: text-[#5FAF78] dark / text-[#4B7C5A] light)
        text?.Classes.Set("InstallmentTextSelected", isSelected);
    }

    /// <summary>
    /// Handle Fund/Sub monthly payout date selection on Step 5.
    /// Sets MonthlyPayoutDate (1-29) on the VM.
    /// </summary>
    private void OnMonthlyDateSelected()
    {
        if (_monthlyDateGrid?.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && int.TryParse(tag, out var day) && Vm != null)
        {
            Vm.MonthlyPayoutDate = day;
        }
    }

    /// <summary>
    /// Handle Fund/Sub weekly payout day selection on Step 5.
    /// Sets WeeklyPayoutDay ("Mon".."Sun") on the VM.
    /// </summary>
    private void OnWeeklyDaySelected()
    {
        if (_weeklyDayList?.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.WeeklyPayoutDay = tag;
        }
    }

    #endregion

    #region Image Picker

    /// <summary>
    /// Open a file picker dialog to select an image for banner or avatar.
    /// </summary>
    private async Task PickImageAsync(bool isBanner)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = isBanner ? "Select Banner Image" : "Select Profile Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" } }
            }
        });

        if (files.Count == 0) return;

        var file = files[0];
        try
        {
            await using var stream = await file.OpenReadAsync();
            // Decode bitmap off the UI thread to avoid blocking during large image loads
            var bitmap = await Task.Run(() => Bitmap.DecodeToWidth(stream, 800));

            if (isBanner)
            {
                var bannerImage = this.FindControl<Image>("BannerPreviewImage");
                if (bannerImage != null)
                {
                    bannerImage.Source = bitmap;
                    bannerImage.IsVisible = true;
                }
            }
            else
            {
                var avatarImage = this.FindControl<Image>("AvatarPreviewImage");
                var avatarIcon = this.FindControl<Control>("AvatarUploadIcon");
                if (avatarImage != null)
                {
                    avatarImage.Source = bitmap;
                    avatarImage.IsVisible = true;
                }
                if (avatarIcon != null)
                {
                    avatarIcon.IsVisible = false;
                }
            }
        }
        catch
        {
            // File read error — silently ignore for prototype
        }
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
    /// Reset all visual state (stepper, type cards, ListBox selections, image previews,
    /// payout frequency, installment checkboxes) so the wizard appears fresh.
    /// Called by MyProjectsView.OpenCreateWizard() after VM.ResetWizard().
    /// </summary>
    public void ResetVisualState()
    {
        // Reset type card visuals
        _selectedType = null;
        ApplyTypeCardStyles();

        // Reset stepper
        UpdateStepper();

        // Clear ListBox selections (Step 4)
        if (_investAmountPresets != null) _investAmountPresets.SelectedIndex = -1;
        if (_fundAmountPresets != null) _fundAmountPresets.SelectedIndex = -1;
        if (_subPricePresets != null) _subPricePresets.SelectedIndex = -1;
        if (_durationPresets != null) _durationPresets.SelectedIndex = -1;

        // Clear ListBox selections (Step 5 - Investment)
        if (_investFrequencyPresets != null) _investFrequencyPresets.SelectedIndex = -1;

        // Clear duration preset button selection (Step 5 - Investment)
        _selectedDurationPresetBtn?.Classes.Set("DurPresetSelected", false);
        _selectedDurationPresetBtn = null;

        // Clear ListBox selections (Step 5 - Fund/Sub)
        if (_monthlyDateGrid != null) _monthlyDateGrid.SelectedIndex = -1;
        if (_weeklyDayList != null) _weeklyDayList.SelectedIndex = -1;

        // Reset payout frequency visuals
        UpdatePayoutFreqVisuals();

        // Reset installment visuals
        UpdateInstallmentVisuals();

        // Reset image previews
        var bannerImage = this.FindControl<Image>("BannerPreviewImage");
        if (bannerImage != null)
        {
            bannerImage.Source = null;
            bannerImage.IsVisible = false;
        }
        var avatarImage = this.FindControl<Image>("AvatarPreviewImage");
        if (avatarImage != null)
        {
            avatarImage.Source = null;
            avatarImage.IsVisible = false;
        }
        var avatarIcon = this.FindControl<Control>("AvatarUploadIcon");
        if (avatarIcon != null) avatarIcon.IsVisible = true;
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

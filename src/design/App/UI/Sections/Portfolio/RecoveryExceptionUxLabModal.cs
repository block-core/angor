#if DEBUG
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using App.UI.Shell;

namespace App.UI.Sections.Portfolio;

public sealed class RecoveryExceptionUxLabModal : UserControl, IBackdropCloseable
{
    private readonly ShellViewModel shellVm;

    public RecoveryExceptionUxLabModal(ShellViewModel shellVm)
    {
        this.shellVm = shellVm;
        Content = BuildContent();
        this.shellVm.PropertyChanged += OnShellPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        shellVm.PropertyChanged -= OnShellPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public void OnBackdropCloseRequested()
    {
        shellVm.HideModal();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsDarkThemeEnabled))
        {
            Content = BuildContent();
        }
    }

    private Control BuildContent()
    {
        Border root = new()
        {
            Classes = { "ModalCard", "Wide" },
            Background = GetBrush("RecoveryModalBg"),
            BorderBrush = GetBrush("RecoveryModalBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24),
            MaxHeight = 760
        };

        StackPanel content = new()
        {
            Spacing = 16
        };

        content.Children.Add(new TextBlock
        {
            Text = "Exception UX Lab",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = GetBrush("TextStrong")
        });

        content.Children.Add(new TextBlock
        {
            Text = "Pick a recovery case, then preview the question, failure, or success state. This is local-only and does not call the SDK.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetBrush("TextMuted")
        });

        foreach (RecoveryLabCase labCase in RecoveryLabCase.All)
        {
            content.Children.Add(BuildCaseRow(labCase));
        }

        Button closeButton = CreateButton("Close", () => shellVm.HideModal());
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        content.Children.Add(closeButton);

        root.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        return root;
    }

    private Control BuildCaseRow(RecoveryLabCase labCase)
    {
        Border card = new()
        {
            Background = GetBrush("RecoveryInfoCardBg"),
            BorderBrush = GetBrush("RecoveryInfoCardBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16)
        };

        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        StackPanel copy = new()
        {
            Spacing = 4
        };

        copy.Children.Add(new TextBlock
        {
            Text = labCase.Title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetBrush("TextStrong")
        });
        copy.Children.Add(new TextBlock
        {
            Text = labCase.Question,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetBrush("TextMuted")
        });

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        buttons.Children.Add(CreateButton("Question", () => OpenRecoveryPreview(labCase, RecoveryLabOutcome.Question)));
        buttons.Children.Add(CreateButton("Error", () => OpenRecoveryPreview(labCase, RecoveryLabOutcome.Error)));
        buttons.Children.Add(CreateButton("Success", () => OpenRecoveryPreview(labCase, RecoveryLabOutcome.Success)));

        Grid.SetColumn(copy, 0);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(copy);
        grid.Children.Add(buttons);
        card.Child = grid;
        return card;
    }

    private Button CreateButton(string text, Action onClick)
    {
        Button button = new()
        {
            Content = text,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(8),
            Background = GetBrush("ModalCancelBg"),
            Foreground = GetBrush("ModalCancelFg"),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        button.Click += (_, _) => onClick();
        return button;
    }

    private void OpenRecoveryPreview(RecoveryLabCase labCase, RecoveryLabOutcome outcome)
    {
        InvestmentViewModel investment = CreateInvestment(labCase, outcome);
        RecoveryModalsView view = new()
        {
            DataContext = investment
        };

        shellVm.ShowModal(view);
    }

    private static InvestmentViewModel CreateInvestment(RecoveryLabCase labCase, RecoveryLabOutcome outcome)
    {
        InvestmentViewModel investment = new()
        {
            ProjectName = "Exception UX Preview Project",
            ShortDescription = "Local preview data for exception and success wording decisions.",
            FundingAmount = "0.12500000 BTC",
            FundingDate = DateTime.UtcNow.AddMonths(-2).ToString("dd MMM yyyy"),
            TypeLabel = "Funding",
            StatusText = "Funding Active",
            StatusClass = "active",
            StatusPill1 = "Funding",
            StatusPill2 = "Funding Active",
            TotalInvested = "0.12500000",
            AvailableToClaim = "0.08250000",
            Spent = "0.04250000",
            Progress = 76,
            Status = "Active",
            ProjectType = "fund",
            Step = 3,
            TargetAmount = "1.00000000",
            TotalRaised = "0.76000000",
            TotalInvestors = 18,
            CurrencySymbol = "BTC",
            StartDate = DateTime.UtcNow.AddMonths(-3).ToString("dd MMM yyyy"),
            EndDate = DateTime.UtcNow.AddMonths(1).ToString("dd MMM yyyy"),
            TransactionDate = DateTime.UtcNow.AddMonths(-2).ToString("dd MMM yyyy"),
            ApprovalStatus = "Approved",
            ProjectIdentifier = "lab-project-id-0000000000000000000000000000000000000000000000000000000000000000",
            InvestmentWalletId = "lab-wallet-id",
            InvestmentTransactionId = "lab-investment-txid",
            RecoveryState = labCase.State,
            PenaltyDuration = "30 days",
            MinerFee = "0.00001250",
            DestinationAddress = "tb1qexceptionuxpreviewdestination0000000000000000000000000000",
            PenaltyDaysRemaining = 9,
            IsRecoveryActionBlocked = outcome == RecoveryLabOutcome.Error,
            IsExceptionUxLabPreview = true,
            ExceptionUxLabShouldSucceed = outcome == RecoveryLabOutcome.Success,
            ExceptionUxLabError = labCase.Error
        };

        investment.Stages = new ObservableCollection<InvestmentStageViewModel>
        {
            new() { StageNumber = 1, StagePrefix = "Payment", Percentage = "25%", ReleaseDate = DateTime.UtcNow.AddMonths(-1).ToString("dd MMM yyyy"), Amount = "0.03125000", Status = labCase.FirstStageStatus },
            new() { StageNumber = 2, StagePrefix = "Payment", Percentage = "35%", ReleaseDate = DateTime.UtcNow.AddDays(15).ToString("dd MMM yyyy"), Amount = "0.04375000", Status = labCase.SecondStageStatus },
            new() { StageNumber = 3, StagePrefix = "Payment", Percentage = "40%", ReleaseDate = DateTime.UtcNow.AddMonths(2).ToString("dd MMM yyyy"), Amount = "0.05000000", Status = labCase.ThirdStageStatus }
        };

        switch (outcome)
        {
            case RecoveryLabOutcome.Question:
                ShowEntryModal(investment, labCase.ActionKey);
                break;
            case RecoveryLabOutcome.Error:
                ShowEntryModal(investment, labCase.ActionKey);
                investment.ErrorMessage = labCase.Error;
                break;
            case RecoveryLabOutcome.Success:
                investment.ShowSuccessModal = true;
                break;
        }

        return investment;
    }

    private static void ShowEntryModal(InvestmentViewModel investment, string actionKey)
    {
        switch (actionKey)
        {
            case "unfundedRelease":
            case "penaltyRelease":
                investment.ShowReleaseModal = true;
                break;
            case "endOfProject":
                investment.ShowClaimModal = true;
                break;
            case "belowThreshold":
            case "recovery":
                investment.ShowRecoveryModal = true;
                break;
        }
    }

    private IBrush GetBrush(string resourceKey)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out object? resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out resource) == true &&
            resource is Color color)
        {
            return new SolidColorBrush(color);
        }

        return Brushes.Transparent;
    }

    private sealed record RecoveryLabCase(
        string Title,
        string ActionKey,
        RecoveryState State,
        string Question,
        string Error,
        string FirstStageStatus,
        string SecondStageStatus,
        string ThirdStageStatus)
    {
        public static readonly IReadOnlyList<RecoveryLabCase> All =
        [
            new(
                "Recover without penalty",
                "unfundedRelease",
                new RecoveryState(true, false, true, false, true),
                "The founder has released signatures. Should the user see this as a safe release action rather than a scary recovery action?",
                "We could not release these funds because the founder signatures no longer match the current transaction state. Refresh and try again.",
                "Released",
                "Not Spent",
                "Not Spent"),
            new(
                "End-of-project claim",
                "endOfProject",
                new RecoveryState(true, false, false, true, true),
                "The project has ended. Should the user see this as a normal claim, not a penalty recovery?",
                "We could not build the claim transaction because the project output was not found on the indexer yet. Refresh in a few minutes and try again.",
                "Released",
                "Not Spent",
                "Not Spent"),
            new(
                "Below-threshold direct recovery",
                "belowThreshold",
                new RecoveryState(true, false, false, false, false),
                "The investment is below the penalty threshold. Should the user see a direct recovery question with no penalty warning?",
                "We could not recover these funds because the wallet needs a fresh change address. Unlock the wallet and try again.",
                "Not Spent",
                "Not Spent",
                "Pending"),
            new(
                "Recover to penalty",
                "recovery",
                new RecoveryState(true, false, false, false, true),
                "The user can recover now, but funds will be locked through the penalty period. What warning copy makes that clear?",
                "We could not recover to penalty because founder signatures were not available from the relays. Try again after relay sync completes.",
                "Not Spent",
                "Not Spent",
                "Pending"),
            new(
                "Recover from penalty",
                "penaltyRelease",
                new RecoveryState(false, true, false, false, true),
                "The penalty period has ended. Should this use release wording and avoid repeating recovery warnings?",
                "We could not release the penalty funds because the recovery transaction has not confirmed on-chain yet.",
                "Recovered (In Penalty)",
                "Penalty can be released",
                "Pending")
        ];
    }

    private enum RecoveryLabOutcome
    {
        Question,
        Error,
        Success
    }
}
#endif

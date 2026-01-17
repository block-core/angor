using System.Collections.ObjectModel;
using ReactiveUI.Validation.Abstractions;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{

    public interface IFundProjectConfig : IProjectProfile
    {

        IAmountUI? GoalAmount { get; set; }


        PayoutFrequency? PayoutFrequency { get; set; }
        ReactiveSelection<int, int> SelectedInstallments { get; }
        int? MonthlyPayoutDate { get; set; }
        DayOfWeek? WeeklyPayoutDay { get; set; }
        IEnumerable<int> AvailableInstallmentCounts { get; }
        IAmountUI? Threshold { get; set; }
    }
}


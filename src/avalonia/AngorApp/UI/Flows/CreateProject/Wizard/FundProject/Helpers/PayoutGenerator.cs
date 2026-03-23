using System.Linq;
using Angor.Shared.Models;
using Angor.Shared.Utilities;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Helpers;

public static class PayoutGenerator
{
    public static IEnumerable<IPayoutConfig> Generate(
        PayoutFrequency frequency,
        int installmentCount,
        DateTime startDate,
        int? monthlyPayoutDay,
        DayOfWeek? weeklyPayoutDay)
    {
        StageFrequency stageFrequency;
        PayoutDayType payoutDayType;
        int payoutDay = 0;

        if (frequency == PayoutFrequency.Monthly)
        {
            stageFrequency = StageFrequency.Monthly;
            payoutDayType = PayoutDayType.SpecificDayOfMonth;
            payoutDay = monthlyPayoutDay ?? 1;
        }
        else
        {
            stageFrequency = StageFrequency.Weekly;
            payoutDayType = PayoutDayType.SpecificDayOfWeek;
            payoutDay = (int)(weeklyPayoutDay ?? DayOfWeek.Monday);
        }

        var pattern = new DynamicStagePattern
        {
            Frequency = stageFrequency,
            PayoutDayType = payoutDayType,
            PayoutDay = payoutDay
        };

        var percent = 1m / installmentCount;

        return Enumerable.Range(0, installmentCount).Select(i =>
        {
            var date = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, i);
            return new PayoutConfig
            {
                Percent = percent,
                PayoutDate = date
            };
        });
    }
}

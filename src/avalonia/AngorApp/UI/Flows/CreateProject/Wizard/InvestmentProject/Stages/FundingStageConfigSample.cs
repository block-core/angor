using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using ReactiveUI.Validation.Contexts;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public class FundingStageConfigSample : ReactiveObject, IFundingStageConfig
    {
        public decimal? Percent { get; set; }
        public DateTime? ReleaseDate { get; set; } = DateTime.Now;
        public TimeSpan? TimeFromPrevious { get; } = TimeSpan.FromDays(30);
        public void SetPreviousDateSource(IObservable<DateTime> source)
        {
        }

        public IObservable<bool> IsValid => Observable.Return(true);
        public IValidationContext ValidationContext { get; } = new ValidationContext();
    }
}
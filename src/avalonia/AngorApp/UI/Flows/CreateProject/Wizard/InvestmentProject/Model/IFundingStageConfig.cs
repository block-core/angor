using System.ComponentModel;
using ReactiveUI.Validation.Abstractions;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public interface IFundingStageConfig : IValidatableViewModel, IValidatable, INotifyPropertyChanged
    {
        public decimal? Percent { get; set; }

        public DateTime? ReleaseDate { get; set; }
        public TimeSpan? TimeFromPrevious { get; }
        void SetPreviousDateSource(IObservable<DateTime> source);
    }
}
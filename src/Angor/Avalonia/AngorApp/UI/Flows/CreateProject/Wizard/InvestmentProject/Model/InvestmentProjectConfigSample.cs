using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages;
using DynamicData;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public class InvestmentProjectConfigSample : ReactiveObject, IInvestmentProjectConfig
    {
        private readonly ReadOnlyObservableCollection<IFundingStageConfig> stages;

        public InvestmentProjectConfigSample()
        {
            StagesSource.AddRange(
            [
                new FundingStageConfigSample { Percent = 33.33m },
                new FundingStageConfigSample { Percent = 33.33m },
                new FundingStageConfigSample { Percent = 33.34m }
            ]);
            StagesSource.Connect().Bind(out stages).Subscribe();
        }

        public string Name { get; set; } = "New Project";
        public string Description { get; set; } = "New project's description";
        public string Website { get; set; } = "https://example.com";
        public IAmountUI? TargetAmount { get; set; } = new MutableAmountUI { Sats = 1000000 };
        public int? PenaltyDays { get; set; } = 30;
        public long? PenaltyThreshold { get; set; } = 0;
        public DateTime? FundingEndDate { get; set; } = DateTime.Now.AddDays(30);
        public DateTime? StartDate { get; set; } = DateTime.Now;
        public DateTime? ExpiryDate { get; set; } = DateTime.Now.AddYears(1);

        public string AvatarUri { get; set; } = "https://example.com/avatar.png";
        public string BannerUri { get; set; } = "https://example.com/banner.png";
        public string Nip05 { get; set; } = "user@domain.com";
        public string Lud16 { get; set; } = "user@domain.com";
        public string Nip57 { get; set; } = "user@domain.com";

        public Angor.Shared.Models.ProjectType ProjectType { get; set; } = Angor.Shared.Models.ProjectType.Invest;

        public ReadOnlyObservableCollection<IFundingStageConfig> Stages => stages;
        private SourceList<IFundingStageConfig> StagesSource { get; } = new();

        public void AddStage()
        {
            StagesSource.Add(new FundingStageConfigSample());
        }

        public void RemoveStage(IFundingStageConfig stage)
        {
            StagesSource.Remove(stage);
        }

        public IFundingStageConfig CreateAndAddStage(decimal percent = 0, DateTime? releaseDate = null)
        {
            var stage = new FundingStageConfigSample { Percent = percent, ReleaseDate = releaseDate };
            StagesSource.Add(stage);
            return stage;
        }


        public IObservable<bool> IsValid => Observable.Return(true);
        public IValidationContext ValidationContext { get; } = new ValidationContext();


#pragma warning disable CS0067
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
#pragma warning restore CS0067
        public System.Collections.IEnumerable GetErrors(string? propertyName) => Enumerable.Empty<object>();
        public bool HasErrors => false;
    }
}
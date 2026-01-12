using System.Collections.ObjectModel;
using DynamicData;
using ReactiveUI.Validation.Abstractions;
using Angor.Shared.Models;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public interface IInvestmentProjectConfig : IValidatableViewModel, IValidatable, System.ComponentModel.INotifyDataErrorInfo
    {
        string Name { get; set; }
        string Description { get; set; }
        string Website { get; set; }
        IAmountUI? TargetAmount { get; set; }
        int? PenaltyDays { get; set; }
        long? PenaltyThreshold { get; set; }
        DateTime? FundingEndDate { get; set; }
        DateTime? StartDate { get; set; }
        DateTime? ExpiryDate { get; set; }

        string AvatarUri { get; set; }
        string BannerUri { get; set; }
        string Nip05 { get; set; }
        string Lud16 { get; set; }
        string Nip57 { get; set; }

        // Assuming ProjectType is needed here, need to add using or fully qualify
        Angor.Shared.Models.ProjectType ProjectType { get; set; }

        ReadOnlyObservableCollection<IFundingStageConfig> Stages { get; }

        void AddStage();
        IFundingStageConfig CreateAndAddStage(decimal percent = 0, DateTime? releaseDate = null);
        void RemoveStage(IFundingStageConfig stage);
    }
}
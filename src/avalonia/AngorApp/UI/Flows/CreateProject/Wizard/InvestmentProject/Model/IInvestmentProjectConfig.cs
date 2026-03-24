using System.Collections.ObjectModel;
using DynamicData;
using ReactiveUI.Validation.Abstractions;
using Angor.Shared.Models;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public interface IInvestmentProjectConfig : IProjectConfig
    {

        DateTime? FundingEndDate { get; set; }
        DateTime? StartDate { get; set; }


        ReadOnlyObservableCollection<IFundingStageConfig> Stages { get; }

        void AddStage();
        IFundingStageConfig CreateAndAddStage(decimal percent = 0, DateTime? releaseDate = null);
        void RemoveStage(IFundingStageConfig stage);
    }
}
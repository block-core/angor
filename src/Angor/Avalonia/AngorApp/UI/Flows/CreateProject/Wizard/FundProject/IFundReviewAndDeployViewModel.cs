using Angor.Sdk.Common;
using AngorApp.UI.Shared;
using System.Collections.ObjectModel;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject
{

    public interface IFundReviewAndDeployViewModel : IHaveTitle
    {
        IEnhancedCommand<Result<string>> DeployCommand { get; }
        IFundProjectConfig NewProject { get; }
        ReadOnlyObservableCollection<IPayoutConfig> Payouts { get; }
    }
}

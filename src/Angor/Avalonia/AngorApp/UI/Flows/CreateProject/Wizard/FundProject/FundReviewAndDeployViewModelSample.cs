using System.Collections.ObjectModel;
using Angor.Sdk.Common;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject;


public class FundReviewAndDeployViewModelSample : IFundReviewAndDeployViewModel
{
    public FundReviewAndDeployViewModelSample()
    {
        NewProject = new FundProjectConfigSample();


        var samplePayouts = new ObservableCollection<IPayoutConfig>
        {
            new PayoutConfigSample { Percent = 0.25m, PayoutDate = DateTime.Now.AddMonths(1) },
            new PayoutConfigSample { Percent = 0.25m, PayoutDate = DateTime.Now.AddMonths(2) },
            new PayoutConfigSample { Percent = 0.25m, PayoutDate = DateTime.Now.AddMonths(3) },
            new PayoutConfigSample { Percent = 0.25m, PayoutDate = DateTime.Now.AddMonths(4) }
        };

        Payouts = new ReadOnlyObservableCollection<IPayoutConfig>(samplePayouts);
    }

    public IEnhancedCommand<Result<string>> DeployCommand { get; } = null!;

    public IFundProjectConfig NewProject { get; }

    public ReadOnlyObservableCollection<IPayoutConfig> Payouts { get; }

    public IObservable<string> Title => Observable.Return("Review & Deploy");
}

using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public class CreateStageSample : ReactiveObject, ICreateStage
    {
        public double Percent { get; set; }
        public DateTime ReleaseDate { get; set; } = DateTime.Now;
        public IAmountUI Amount { get; set; } = AmountUI.FromBtc(0.123);
    }
}
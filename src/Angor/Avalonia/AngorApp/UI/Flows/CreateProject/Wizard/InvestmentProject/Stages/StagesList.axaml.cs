using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public partial class StagesList : UserControl
    {
        public static readonly StyledProperty<IEnumerable<AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model.ICreateStage>> StagesProperty =
            AvaloniaProperty.Register<StagesList, IEnumerable<AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model.ICreateStage>>(nameof(Stages));

        public IEnumerable<AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model.ICreateStage> Stages
        {
            get => GetValue(StagesProperty);
            set => SetValue(StagesProperty, value);
        }

        public StagesList()
        {
            InitializeComponent();
        }
    }
}
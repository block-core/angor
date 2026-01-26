namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public partial class StagesList : UserControl
    {
        public static readonly StyledProperty<IEnumerable<Model.IFundingStageConfig>> StagesProperty = AvaloniaProperty.Register<StagesList, IEnumerable<Model.IFundingStageConfig>>(nameof(Stages));

        public IEnumerable<Model.IFundingStageConfig> Stages
        {
            get => GetValue(StagesProperty);
            set => SetValue(StagesProperty, value);
        }

        public static readonly StyledProperty<long?> TotalSatsProperty = AvaloniaProperty.Register<StagesList, long?>(nameof(TotalSats));

        public long? TotalSats
        {
            get => GetValue(TotalSatsProperty);
            set => SetValue(TotalSatsProperty, value);
        }

        public StagesList()
        {
            InitializeComponent();
        }
    }
}
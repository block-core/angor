using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;
using System.Collections.Generic;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Payouts
{
    public partial class PayoutsList : UserControl
    {
        public static readonly StyledProperty<IEnumerable<IPayoutConfig>> PayoutsProperty =
            AvaloniaProperty.Register<PayoutsList, IEnumerable<IPayoutConfig>>(nameof(Payouts));

        public static readonly StyledProperty<long?> TotalSatsProperty =
            AvaloniaProperty.Register<PayoutsList, long?>(nameof(TotalSats));

        public static readonly StyledProperty<PayoutFrequency?> FrequencyProperty =
            AvaloniaProperty.Register<PayoutsList, PayoutFrequency?>(nameof(Frequency));

        public IEnumerable<IPayoutConfig> Payouts
        {
            get => GetValue(PayoutsProperty);
            set => SetValue(PayoutsProperty, value);
        }

        public long? TotalSats
        {
            get => GetValue(TotalSatsProperty);
            set => SetValue(TotalSatsProperty, value);
        }

        public PayoutFrequency? Frequency
        {
            get => GetValue(FrequencyProperty);
            set => SetValue(FrequencyProperty, value);
        }

        public PayoutsList()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

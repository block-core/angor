using System.Linq;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Controls.Feerate;

public partial class Controller : ReactiveValidationObject
{
    [Reactive] private long? amount;
    [Reactive] private IFeeCalculator? feeCalculator;
    [ObservableAsProperty] private long? feerate;
    [ObservableAsProperty] private IEnumerable<IFeerateViewModel>? feeRates;
    [Reactive] private IFeerateViewModel? selectedFeeRate;
    [Reactive] private IEnumerable<IFeeratePreset>? userProvided;

    public Controller()
    {
        this.ValidationRule(x => x.Feerate, d => d > 0 && d < 1000, "Invalid feerate");

        feeRatesHelper = this.WhenAnyValue<Controller, (IFeeCalculator? calculator, IEnumerable<IFeeratePreset>? presets, long? amount), IFeeCalculator?, IEnumerable<IFeeratePreset>?, long?>(
                x => x.FeeCalculator,
                controller => controller.UserProvided,
                controller => controller.Amount, (calculator, presets, amt) => (calculator, presets, amount: amt))
            .Select(a =>
            {
                var argCalculator = a.calculator;

                IEnumerable<IFeeratePreset> presets;
                if (a.presets != null)
                {
                    presets = a.presets.Select(preset => (IFeeratePreset)new Preset(preset.Name, preset.Feerate, amount.HasValue ? new AmountUI(amount.Value) : null, feeCalculator));
                }
                else
                {
                    presets = [];
                }

                var argAmount = a.amount;
                var custom = new CustomFeeRate(argAmount, argCalculator);

                return presets.Append<IFeerateViewModel>(custom);
            }).ToProperty(this, controller => controller.FeeRates);

        var selectedFeeRates = this.WhenAnyValue<Controller, long?>(x => x.SelectedFeeRate!.Feerate.Sats).Where(l => l != 0);

        feerateHelper = selectedFeeRates.ToProperty(this, x => x.Feerate);
    }
}
using AngorApp.UI.Controls.Feerate;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI;

namespace AngorApp.UI.Controls.Common;

internal partial class FeerateSelectionViewModel : ReactiveValidationObject, IValidatable
{
    private readonly UIServices services;
    [Reactive] private IFeeCalculator? feeCalculator;
    [Reactive] private long? feerate;
    [Reactive] private long? amount;

    public FeerateSelectionViewModel(UIServices services)
    {
        this.services = services;
        this.ValidationRule(
            this.WhenAnyValue(x => x.Feerate),
            feerate => feerate.HasValue && feerate.Value > 0,
            _ => "Feerate must be a positive value"
        );
    }

    public IEnumerable<IFeeratePreset> Presets => services.FeeratePresets;

    public IObservable<bool> IsValid => this.IsValid();
}
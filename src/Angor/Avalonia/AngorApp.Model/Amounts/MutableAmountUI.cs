using AngorApp.Model.Contracts.Amounts;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Model.Amounts;

/// <summary>
/// Mutable implementation of IAmountUI using ReactiveUI for two-way binding scenarios.
/// </summary>
public partial class MutableAmountUI : ReactiveObject, IAmountUI
{
    [Reactive] private long sats;

    public string Symbol { get; init; } = AmountUI.DefaultSymbol;

    // Explicit interface implementation to satisfy IAmountUI.Sats (read-only)
    long IAmountUI.Sats => Sats;
}

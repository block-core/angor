using Zafiro.Avalonia.Dialogs;
using Option = Zafiro.Avalonia.Dialogs.Option;
using AngorApp.UI.Shell;

namespace AngorApp.UI.Flows.InvestV2.InvestmentResult;

public class InvestResultViewModel(IShellViewModel shell) : IInvestResultViewModel
{
    public IAmountUI Amount { get; set; } = AmountUI.FromBtc(0.2);

    public IEnumerable<IOption> Options(ICloseable closeable)
    {
        return [new Option("View My Investments", EnhancedCommand.Create(() =>
        {
            closeable.Close();
            shell.SetSection("Funded");
        }), new Settings())];
    }
}
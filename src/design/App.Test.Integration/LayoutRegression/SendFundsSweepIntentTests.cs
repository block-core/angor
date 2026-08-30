using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Sections.Funds;
using FluentAssertions;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

public class SendFundsSweepIntentTests
{
    [AvaloniaFact]
    public void Hundred_percent_sets_sweep_and_manual_edit_clears_it()
    {
        var modal = new SendFundsModal();
        modal.SetWallet("Wallet", "On-Chain", "0.00100000 BTC", "wallet-id");
        var window = new Window { Width = 390, Height = 800, Content = modal };

        try
        {
            window.Show();
            window.UpdateLayout();

            Button hundred = modal.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "BtnPct100");
            hundred.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            modal.IsSweepAll.Should().BeTrue();
            modal.FindControl<TextBox>("AmountInput")!.Text.Should().Be("0.00100000");

            modal.FindControl<TextBox>("AmountInput")!.Text = "0.00050000";
            Dispatcher.UIThread.RunJobs();
            modal.IsSweepAll.Should().BeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Partial_percentage_clears_sweep_intent()
    {
        var modal = new SendFundsModal();
        modal.SetWallet("Wallet", "On-Chain", "0.00100000 BTC", "wallet-id");
        var window = new Window { Width = 390, Height = 800, Content = modal };

        try
        {
            window.Show();
            window.UpdateLayout();

            Button hundred = modal.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "BtnPct100");
            hundred.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Button half = modal.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "BtnPct50");
            half.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            modal.IsSweepAll.Should().BeFalse();
            modal.FindControl<TextBox>("AmountInput")!.Text.Should().Be("0.00050000");
        }
        finally
        {
            window.Close();
        }
    }
}

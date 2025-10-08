namespace Angor.UI.Model.Flows;

public interface ISendMoneyFlow
{
    Task SendMoney(IWallet sourceWallet);
}
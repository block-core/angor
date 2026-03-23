namespace AngorApp.Model.Contracts.Flows;

public interface ISendMoneyFlow
{
    Task SendMoney(IWallet sourceWallet);
}
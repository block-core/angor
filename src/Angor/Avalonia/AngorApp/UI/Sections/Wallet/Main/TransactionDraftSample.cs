using Angor.Sdk.Wallet.Domain;

namespace AngorApp.UI.Sections.Wallet.Main;

public class TransactionDraftSample : ITransactionDraft
{
    public string Address { get; set; }
    
    public long FeeRate { get; set; }

    public IAmountUI TotalFee { get; set; }
    public long Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }
    
    public async Task<Result<TxId>> Confirm()
    {
        await Task.Delay(3000);
        return new TxId("test");
    }
}
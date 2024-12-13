using AngorApp.Sections.Wallet;

namespace AngorApp.Sections.Browse.Details.Invest.TransactionPreview;

public class TransactionPreviewViewModel : ReactiveObject
{
    public ITransaction Transaction { get; }

    public TransactionPreviewViewModel(ITransaction transaction)
    {
        Transaction = transaction;
    }
}
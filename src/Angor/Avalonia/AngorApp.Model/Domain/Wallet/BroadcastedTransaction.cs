using System.Text.Json;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Model.Contracts.Amounts;
using AngorApp.Model.Contracts.Wallet;
using AngorApp.Model.Domain.Amounts;
using AngorApp.Model.Domain.Common;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI.Commands;

namespace AngorApp.Model.Domain.Wallet;

public class HistoryTransaction : IBroadcastedTransaction
{
    public HistoryTransaction(BroadcastedTransaction transaction, IDialog dialog)
    {
        AllOutputs = transaction.AllOutputs;
        AllInputs = transaction.AllInputs;
        WalletOutputs = transaction.WalletOutputs;
        WalletInputs = transaction.WalletInputs;
        Id = transaction.Id;
        TotalFee = transaction.Fee;
        Balance = new AmountUI(transaction.GetBalance().Sats);
        BlockTime = transaction.BlockTime;
        RawJson = transaction.RawJson;

        ShowJson = ReactiveCommand.CreateFromTask(() => dialog.Show(new LongTextViewModel()
        {
            Text = FormatJson(RawJson),
        }, "Transaction JSON", System.Reactive.Linq.Observable.Return(true))).Enhance();
    }

    public IEnumerable<TransactionOutput> AllOutputs { get; }

    public IEnumerable<TransactionInput> AllInputs { get; }

    public IEnumerable<TransactionOutput> WalletOutputs { get; }

    public IEnumerable<TransactionInput> WalletInputs { get; }
    
    public string Id { get; }
    
    public long TotalFee { get; }
    public IAmountUI Balance { get; }
    public DateTimeOffset? BlockTime { get; }
    public IEnhancedCommand ShowJson { get; }
    public string RawJson { get; }
    
    private string FormatJson(string json)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(json);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var element = jsonDocument.RootElement;
            return JsonSerializer.Serialize(element.Clone(), options);
        }
        catch (JsonException)
        {
            return "Invalid JSON: " + json;
        }
    }
}

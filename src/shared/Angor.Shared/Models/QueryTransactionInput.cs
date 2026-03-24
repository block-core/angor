namespace Angor.Shared.Models
{
   public class QueryTransactionInput
   {
      /// <summary>
      /// Gets or sets the input index.
      /// </summary>
      public int InputIndex { get; set; }

      /// <summary>
      /// Gets or sets the addresses.
      /// </summary>
      public string InputAddress { get; set; }
      public long InputAmount { get; set; }

      /// <summary>
      /// Gets or sets the coinbase id the transaction is the first transaction 'coinbase'.
      /// </summary>
      public string CoinBase { get; set; }

      /// <summary>
      /// Gets or sets the transaction id.
      /// </summary>
      public string InputTransactionId { get; set; }

      public string ScriptSig { get; set; }

      public string ScriptSigAsm { get; set; }

      public string WitScript { get; set; }

      public string SequenceLock { get; set; }
   }
}

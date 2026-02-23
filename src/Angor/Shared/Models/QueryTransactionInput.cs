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
      public string InputAddress { get; set; } = string.Empty;
      public long InputAmount { get; set; }

      /// <summary>
      /// Gets or sets the coinbase id the transaction is the first transaction 'coinbase'.
      /// </summary>
      public string CoinBase { get; set; } = string.Empty;

      /// <summary>
      /// Gets or sets the transaction id.
      /// </summary>
      public string InputTransactionId { get; set; } = string.Empty;

      public string ScriptSig { get; set; } = string.Empty;

      public string ScriptSigAsm { get; set; } = string.Empty;

      public string WitScript { get; set; } = string.Empty;

      public string SequenceLock { get; set; } = string.Empty;
   }
}

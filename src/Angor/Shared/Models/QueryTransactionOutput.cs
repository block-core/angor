namespace Angor.Shared.Models
{
   public class QueryTransactionOutput
   {
      /// <summary>
      /// Gets or sets the addresses.
      /// </summary>
      public string Address { get; set; } = string.Empty;

      /// <summary>
      /// Gets or sets the amount.
      /// </summary>
      public long Balance { get; set; }

      /// <summary>
      /// Gets or sets the input index.
      /// </summary>
      public int Index { get; set; }

      /// <summary>
      /// Gets or sets the output type.
      /// </summary>
      public string OutputType { get; set; } = string.Empty;

      public string ScriptPubKeyAsm { get; set; } = string.Empty;

      public string ScriptPubKey { get; set; } = string.Empty;

      public string SpentInTransaction { get; set; } = string.Empty;
   }
}

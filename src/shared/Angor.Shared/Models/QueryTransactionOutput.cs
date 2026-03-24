namespace Angor.Shared.Models
{
   public class QueryTransactionOutput
   {
      /// <summary>
      /// Gets or sets the addresses.
      /// </summary>
      public string Address { get; set; }

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
      public string OutputType { get; set; }

      public string ScriptPubKeyAsm { get; set; }

      public string ScriptPubKey { get; set; }

      public string SpentInTransaction { get; set; }
   }
}

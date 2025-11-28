namespace Angor.Shared.Models;

public class StageTransactionInput
{
    public string TransactionHex { get; set; }

    public int StageNumber { get; set; }

    public StageTransactionInput(string transactionHex, int stageNumber)
    {
        TransactionHex = transactionHex ?? throw new ArgumentNullException(nameof(transactionHex));

        if (stageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(stageNumber), "Stage number must be greater than 0");

        StageNumber = stageNumber;
    }
}

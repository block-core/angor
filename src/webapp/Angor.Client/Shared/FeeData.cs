using Angor.Client.Storage;
using Angor.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace Angor.Client.Shared
{
    public class FeeData
    {
        public FeeEstimations FeeEstimations { get; set; } = new();
        public FeeEstimation SelectedFeeEstimation { get; set; } = new();
        public int FeePosition { get; set; } = 1;
        public int FeeMin { get; set; } = 1;
        public int FeeMax { get; set; } = 3;

        public void SelectFee(int position)
        {
            FeePosition = position;
            SelectedFeeEstimation = FeeEstimations.Fees
                .OrderBy(fee => fee.Confirmations)
                .ToList()[FeePosition - 1];
        }

        public void SetCustomFee(long customFee)
        {
            SelectedFeeEstimation = new FeeEstimation
            {
                FeeRate = customFee,
                Confirmations = 0 // Custom fee doesn't have block count
            };
        }
    }
}

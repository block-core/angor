using System.Threading.Tasks;
using AngorApp.UI.Shared.Controls;
using AngorApp.UI.Shared.Controls.Feerate;

namespace AngorApp.Design;

public class FeeCalculatorDesignTime : IFeeCalculator
{
    public async Task<Result<long>> GetFee(long feerate, long amount)
    {
        return Result.Success(feerate * 10);
    }
}
using System.Threading.Tasks;
using AngorApp.UI.Controls;

namespace AngorApp;

public class FeeCalculatorDesignTime : IFeeCalculator
{
    public async Task<Result<long>> GetFee(long feerate, long amount)
    {
        return Result.Success(feerate * 10);
    }
}
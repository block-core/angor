using System.Threading.Tasks;
using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;

namespace AngorApp.Design;

public class FeeCalculatorDesignTime : IFeeCalculator
{
    public async Task<Result<long>> GetFee(long feerate, long amount)
    {
        return Result.Success(feerate * 10);
    }
}
using System.Threading.Tasks;
using AngorApp.UI.Shared.Controls;
using AngorApp.UI.Shared.Controls.Feerate;

namespace AngorApp.Design;

public class FeeCalculatorDesignTime : IFeeCalculator
{
    public Task<Result<long>> GetFee(long feerate, long amount)
    {
        return Task.FromResult(Result.Success(feerate * 10));
    }
}
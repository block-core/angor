using System.Threading.Tasks;

namespace AngorApp.UI.Shared.Services;

public interface IValidations
{
    Task<Result> CheckNip05Username(string username, string nostrPubKey);
    Task<Result> CheckLightningAddress(string lightningAddress);
    Task<Result<bool>> IsImage(string url);
}
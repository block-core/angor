using System.Threading.Tasks;

namespace AngorApp.UI.Services;

public interface IValidations
{
    Task<Result> CheckNip05Username(string username, string nostrPubKey);
    Task<Result> CheckLightningAddress(string lightningAddress);
}
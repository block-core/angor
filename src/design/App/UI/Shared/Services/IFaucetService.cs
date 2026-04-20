using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace App.UI.Shared.Services;

/// <summary>
/// Sends test coins to a signet address from a faucet.
/// Abstracted so integration tests can swap in the local docker faucet
/// (see <c>src/design/App.Test.Integration/docker</c>) without touching
/// ViewModels.
/// </summary>
public interface IFaucetService
{
    Task<Result> RequestCoinsAsync(string address, decimal amountBtc, CancellationToken cancellationToken = default);
}

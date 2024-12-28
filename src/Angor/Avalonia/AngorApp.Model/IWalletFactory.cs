using System.Threading.Tasks;
using AngorApp.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.NoWallet;

public interface IWalletFactory
{
    public Task<Result<IWallet>> Recover();
    public Task<Result<IWallet>> Create();
}
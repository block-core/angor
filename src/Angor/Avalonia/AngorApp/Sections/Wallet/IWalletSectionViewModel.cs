using System.Windows.Input;
using AngorApp.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public interface IWalletSectionViewModel
{
    ReactiveCommand<Unit, Maybe<Result<IWallet>>> CreateWallet { get; }
    ReactiveCommand<Unit, Maybe<Result<IWallet>>> RecoverWallet { get; }
}
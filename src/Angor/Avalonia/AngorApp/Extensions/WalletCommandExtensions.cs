using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Reactive.Linq;
using AngorApp.UI.Services;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Extensions;

public static class WalletCommand
{
    /// <summary>
    /// Creates a ReactiveCommand that requires an active wallet to execute.
    /// The command will automatically obtain the wallet and pass it to the execution function.
    /// </summary>
    public static ReactiveCommand<Unit, Result<T>> Create<T>(
        Func<IWallet, Task<Result<T>>> execute,
        IWalletRoot walletRoot)
    {
        return ReactiveCommand.CreateFromTask(async () =>
        {
            var walletResult = await walletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("No wallet available"));
            
            if (walletResult.IsFailure)
            {
                return Result.Failure<T>(walletResult.Error);
            }
            
            return await execute(walletResult.Value);
        });
    }
    
    /// <summary>
    /// Creates a ReactiveCommand that requires an active wallet to execute, with a canExecute observable.
    /// </summary>
    public static ReactiveCommand<Unit, Result<T>> Create<T>(
        Func<IWallet, Task<Result<T>>> execute,
        IWalletRoot walletRoot,
        IObservable<bool> canExecute)
    {
        return ReactiveCommand.CreateFromTask(async () =>
        {
            var walletResult = await walletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("No wallet available"));
            
            if (walletResult.IsFailure)
            {
                return Result.Failure<T>(walletResult.Error);
            }
            
            return await execute(walletResult.Value);
        }, canExecute);
    }
    
    public static ReactiveCommand<Unit, Maybe<Result<T>>> Create<T>(
        Func<IWallet, Task<Maybe<Result<T>>>> execute,
        IWalletRoot walletRoot,
        IObservable<bool> canExecute)
    {
        return ReactiveCommand.CreateFromTask(async () =>
        {
            var walletResult = await walletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("No wallet available"));
            
            if (walletResult.IsFailure)
            {
                return Result.Failure<T>(walletResult.Error);
            }
            
            return await execute(walletResult.Value);
        }, canExecute);
    }
    
    public static ReactiveCommand<Unit, Maybe<Result>> Create(
        Func<IWallet, Task<Maybe<Result>>> execute,
        IWalletRoot walletRoot,
        IObservable<bool> canExecute)
    {
        return ReactiveCommand.CreateFromTask(async () =>
        {
            var walletResult = await walletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("No wallet available"));
            
            if (walletResult.IsFailure)
            {
                return Result.Failure(walletResult.Error);
            }
            
            return await execute(walletResult.Value);
        }, canExecute);
    }
    
    /// <summary>
    /// Creates a ReactiveCommand with parameters that requires an active wallet to execute.
    /// </summary>
    public static ReactiveCommand<TParam, Result<TResult>> Create<TParam, TResult>(
        Func<IWallet, TParam, Task<Result<TResult>>> execute,
        IWalletRoot walletRoot)
    {
        return ReactiveCommand.CreateFromTask<TParam, Result<TResult>>(async param =>
        {
            var walletResult = await walletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("No wallet available"));
            
            if (walletResult.IsFailure)
            {
                return Result.Failure<TResult>(walletResult.Error);
            }
            
            return await execute(walletResult.Value, param);
        });
    }
}

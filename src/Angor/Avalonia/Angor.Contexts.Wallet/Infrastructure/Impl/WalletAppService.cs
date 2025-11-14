using System.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.History;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Blockcore.NBitcoin.BIP39;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletAppService(
    ISensitiveWalletDataProvider sensitiveWalletDataProvider,
    IWalletFactory walletFactory,
    IWalletOperations walletOperations,
    IWalletStore walletStore,
    IPsbtOperations psbtOperations,
    ITransactionHistory transactionHistory,
    IHttpClientFactory httpClientFactory,
    IWalletAccountBalanceService accountBalanceService)
    : IWalletAppService
{
    //public static readonly WalletId SingleWalletId = new("8E3C5250-4E26-4A13-8075-0A189AEAF793");
    private const string SingleWalletName = "<default>";

    public Task<Result<IEnumerable<WalletMetadata>>> GetMetadatas()
    {
        var result =  accountBalanceService.GetAllAccountBalancesAsync();
        return result.Map(list => list.Select(info => new WalletMetadata(
            SingleWalletName,
            new WalletId(info.AccountInfo.walletId)
        )));
    }

    public async Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId)
    {
        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<IEnumerable<BroadcastedTransaction>>(sensitiveDataResult.Error);
            }
            
            return await transactionHistory.GetTransactions(walletId);
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<BroadcastedTransaction>>($"Error getting transactions: {ex.Message}");
        }
    }
    
    public Task<Result<Balance>> GetBalance(WalletId walletId)
    {
        return GetTransactions(walletId).Map(txns => txns.Sum(x => x.GetBalance().Sats)).Map(l => new Balance(l));
    }

    public async Task<Result<FeeAndSize>> EstimateFeeAndSize(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate)
    {
        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<FeeAndSize>(sensitiveDataResult.Error);
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var accountBalanceInfo = await accountBalanceService.RefreshAccountBalanceInfoAsync(walletId.Value);

            if (accountBalanceInfo.IsFailure)
                return Result.Failure<FeeAndSize>(accountBalanceInfo.Error);
            
            var accountInfo = accountBalanceInfo.Value.AccountInfo;
            
            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (string.IsNullOrEmpty(changeAddress))
                return Result.Failure<FeeAndSize>("No change address available");

            var satsPerVirtualKb = feeRate.SatsPerVByte * 1000;
            var sendInfo = new SendInfo
            {
                FeeRate = satsPerVirtualKb,
                SendAmount = amount.Sats,
                SendToAddress = address.Value,
                ChangeAddress = changeAddress,
                SendUtxos = walletOperations.FindOutputsForTransaction(amount.Sats, accountInfo)
                    .ToDictionary(data => data.UtxoData.outpoint.ToString(), data => data),
            };
            
            var feeCalculationResult = await CalculateTransactionFee(sendInfo, accountInfo, walletWords, feeRate);
            if (!feeCalculationResult.IsSuccess)
            {
                return Result.Failure<FeeAndSize>("Could not calculate transaction fee: " + feeCalculationResult.Error);
            }

            return Result.Success(new FeeAndSize(feeCalculationResult.Value.Fee, feeCalculationResult.Value.VirtualSize));
        }
        catch (Exception ex)
        {
            return Result.Failure<FeeAndSize>($"Error estimating fee: {ex.Message}");
        }
    }
    
    public async Task<Result<Address>> GetNextReceiveAddress(WalletId walletId)
    {
        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<Address>(sensitiveDataResult.Error);
            }

            var accountBalanceInfo = await accountBalanceService.RefreshAccountBalanceInfoAsync(walletId.Value);

            if (accountBalanceInfo.IsFailure)
                return Result.Failure<Address>(accountBalanceInfo.Error);  
            
            var accountInfo = accountBalanceInfo.Value.AccountInfo;

            var address = accountInfo.GetNextReceiveAddress();
            
            if (string.IsNullOrEmpty(address))
            {
                return Result.Failure<Address>("No address available");
            }

            return Result.Success(new Address(address));
        }
        catch (Exception ex)
        {
            return Result.Failure<Address>($"Error getting receive address: {ex.Message}");
        }
    }

    public async Task<Result<TxId>> SendAmount(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate)
    {
        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<TxId>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase.GetValueOrDefault("") };
            var accountBalanceInfo = await accountBalanceService.RefreshAccountBalanceInfoAsync(walletId.Value);

            if (accountBalanceInfo.IsFailure)
                return Result.Failure<TxId>(accountBalanceInfo.Error);
            
            var accountInfo = accountBalanceInfo.Value.AccountInfo;
            
            var satsPerVirtualKb = feeRate.SatsPerVByte * 1000;
            
            var sendInfo = new SendInfo
            {
                SendAmount = amount.Sats,
                SendToAddress = address.Value,
                FeeRate = satsPerVirtualKb,
                ChangeAddress = accountInfo.GetNextChangeReceiveAddress(),
                SendUtxos = walletOperations.FindOutputsForTransaction(amount.Sats, accountInfo)
                    .ToDictionary(data => data.UtxoData.outpoint.ToString(), data => data),
            };
            
            // Calculate the real transaction fee using PSBT operations (following Wallet.razor pattern)
            var feeCalculationResult = await CalculateTransactionFee(sendInfo, accountInfo, walletWords, feeRate);
            if (feeCalculationResult.IsSuccess)
            {
                sendInfo.SendFee = (decimal)feeCalculationResult.Value.Fee;
            }
            else
            {
                return Result.Failure<TxId>("Could not calculate transaction fee: " + feeCalculationResult.Error);
            }
            
            var result = await walletOperations.SendAmountToAddress(walletWords, sendInfo);
            if (!result.Success)
                return Result.Failure<TxId>(result.Message);

            return Result.Success(new TxId(result.Data.GetHash().ToString()));
        }
        catch (Exception ex)
        {
            return Result.Failure<TxId>($"Error sending amount: {ex.Message}");
        }
    }

    public Task<Result<WalletId>> CreateWallet(string name, string seedWords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network)
    {
        return walletFactory.CreateWallet(name ?? SingleWalletName, seedWords, passphrase, encryptionKey, network)
            .Map(_ => _.Id);
    }
    
    public async Task<Result> DeleteWallet(WalletId walletId)
    {
        var walletsResult = await walletStore.GetAll();
        if (walletsResult.IsFailure)
        {
            return Result.Failure(walletsResult.Error);
        }

        var wallets = walletsResult.Value.ToList();
        wallets.RemoveAll(wallet => wallet.Id == walletId.Value);

        var saveResult = await walletStore.SaveAll(wallets);
        if (saveResult.IsFailure)
        {
            return Result.Failure(saveResult.Error);
        }

        sensitiveWalletDataProvider.RemoveSensitiveData(walletId);

        return Result.Success();
    }

    public string GenerateRandomSeedwords()
    {
        var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
        return mnemonic.ToString();
    }

    public async Task<Result> GetTestCoins(WalletId walletId)
    {
        try
        {
            var balanceResult = await GetBalance(walletId);
            if (balanceResult.IsFailure)
            {
                return Result.Failure("Cannot get wallet balance");
            }

            // Check if balance is already too high (more than 100 BTC in testnet)
            if (balanceResult.Value.Sats > 100_00000000)
            {
                return Result.Failure("You already have too much test coins!");
            }

            var addressResult = await GetNextReceiveAddress(walletId);
            if (addressResult.IsFailure)
            {
                return addressResult.ConvertFailure();
            }

            var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync($"https://faucettmp.angor.io/api/faucet/send/{addressResult.Value.Value}");
            
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure($"Faucet request failed: {response.ReasonPhrase}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error getting test coins: {ex.Message}");
        }
    }

    private Task<Result<(long Fee, long VirtualSize)>> CalculateTransactionFee(SendInfo sendInfo, AccountInfo accountInfo, WalletWords walletWords, DomainFeeRate feeRate)
    {
        try
        {
            var unsignedTransaction = walletOperations.CreateSendTransaction(sendInfo, accountInfo);
            
            var psbt = psbtOperations.CreatePsbtForTransaction(
                unsignedTransaction, 
                accountInfo, 
                feeRate.SatsPerVByte, 
                utxoDataWithPaths: sendInfo.SendUtxos.Values.ToList()
            );
            
            var signedTransaction = psbtOperations.SignPsbt(psbt, walletWords);
            var realFeeInSatoshis = signedTransaction.TransactionFee;
            var virtualSize = (long)signedTransaction.Transaction.GetVirtualSize(4);

            return Task.FromResult(Result.Success((realFeeInSatoshis, virtualSize)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<(long Fee, long VirtualSize)>($"Error calculating transaction fee: {ex.Message}"));
        }
    }
}

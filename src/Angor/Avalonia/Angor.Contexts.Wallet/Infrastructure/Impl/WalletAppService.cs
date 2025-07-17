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
    ITransactionHistory transactionHistory)
    : IWalletAppService
{
    public static readonly WalletId SingleWalletId = new(new Guid("8E3C5250-4E26-4A13-8075-0A189AEAF793"));
    private const string SingleWalletName = "<default>";

    public Task<Result<IEnumerable<WalletMetadata>>> GetMetadatas()
    {
        List<WalletMetadata> singleWalletList = [new(SingleWalletName, SingleWalletId)];
        return walletStore.GetAll().Map(wallets => wallets.Any() ? singleWalletList : []).Map(metadatas => metadatas.AsEnumerable());
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

            var sensitiveData = sensitiveDataResult.Value;
            
            var walletWords = sensitiveData.ToWalletWords();
            
            return await transactionHistory.GetTransactions(walletWords);
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
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        
            // Update AccountInfo to ensure it has change addresses
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        
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
        if (walletId != SingleWalletId)
            return Result.Failure<Address>("Invalid wallet ID");

        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<Address>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase.GetValueOrDefault("") };
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

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
        if (walletId != SingleWalletId)
            return Result.Failure<TxId>("Invalid wallet ID");
        
        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<TxId>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase.GetValueOrDefault("") };
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
            
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
        return walletFactory.CreateWallet(SingleWalletName, seedWords, passphrase, encryptionKey, network)
            .Map(_ => SingleWalletId);
    }

    public string GenerateRandomSeedwords()
    {
        var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
        return mnemonic.ToString();
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
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.Networks;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Tests.Funding.TestDoubles;

/// <summary>
/// Angornet miner faucet for integration tests.
/// Uses the miner wallet to fund test addresses automatically.
/// 
/// Miner Wallet Words: "radio diamond leg loud street announce guitar video shiver speed eyebrow"
/// 
/// This class provides utilities to:
/// - Get UTXOs from the miner wallet
/// - Send funds to test addresses
/// - Wait for transaction confirmation
/// </summary>
public class AngornetMinerFaucet
{
    private readonly IWalletOperations _walletOperations;
    private readonly IIndexerService _indexerService;
    private readonly Network _network;
    private readonly ILogger _logger;

    private const string MinerWalletWords = "pretty exhibit model gossip skull picnic humor nasty knee fly source gift"; 
        //"margin radio diamond leg loud street announce guitar video shiver speed eyebrow";
    private const string MinerWalletPassphrase = "";

    public AngornetMinerFaucet(
        IWalletOperations walletOperations,
        IIndexerService indexerService,
        Network network,
        ILogger logger)
    {
        _walletOperations = walletOperations;
        _indexerService = indexerService;
        _network = network;
        _logger = logger;
    }

    /// <summary>
    /// Sends funds from the miner wallet to a test address.
    /// Uses the first available UTXO from the miner wallet.
    /// </summary>
    /// <param name="toAddress">Destination address to fund</param>
    /// <param name="amountSats">Amount to send in satoshis</param>
    /// <param name="feeRate">Fee rate in sats/vB (default: 10)</param>
    /// <returns>Transaction ID of the funding transaction</returns>
    public async Task<string> SendFundsToAddressAsync(string toAddress, long amountSats, long feeRate = 10)
    {
        _logger.LogInformation("Funding address {Address} with {Amount} sats from miner wallet", toAddress, amountSats);

        // Build miner account info
        var minerWords = new WalletWords { Words = MinerWalletWords, Passphrase = MinerWalletPassphrase };
        var minerAccountInfo = _walletOperations.BuildAccountInfoForWalletWords(minerWords);

        await _walletOperations.UpdateDataForExistingAddressesAsync(minerAccountInfo);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(minerAccountInfo);
        
        // Manually generate addresses without fetching all balances (miner has thousands!)
        // Just generate first 20 addresses and check them one by one
        _logger.LogInformation("Generating miner addresses to find UTXOs...");
        
        AddressInfo? fundedAddress = null;
        var accountExtPubKey = ExtPubKey.Parse(minerAccountInfo.ExtPubKey, _network);
        
        for (int i = 0; i < 20; i++)
        {
            // Generate address manually (replicate GenerateAddressFromPubKey logic)
            var addressInfo = GenerateAddress(accountExtPubKey, i, isChange: false);
            
            // Try to fetch UTXOs for this specific address
            var utxos = await _indexerService.FetchUtxoAsync(addressInfo.Address, 0, 1);
            if (utxos != null && utxos.Any())
            {
                addressInfo.UtxoData = utxos;
                minerAccountInfo.AddressesInfo.Add(addressInfo);
                fundedAddress = addressInfo;
                _logger.LogInformation("Found miner address with UTXOs: {Address} (index {Index}), UTXO count: {Count}", 
                    addressInfo.Address, i, utxos.Count);
                break;
            }
            
            _logger.LogDebug("Address {Address} (index {Index}) has no UTXOs, trying next...", addressInfo.Address, i);
        }

        if (fundedAddress == null)
        {
            throw new InvalidOperationException("Miner wallet has no UTXOs available. Please mine some blocks first.");
        }

        // Get top 1 UTXO (the first one)
        var utxo = fundedAddress.UtxoData.First();
        _logger.LogInformation("Using UTXO: {Outpoint}, Value: {Value} sats", utxo.outpoint, utxo.value);

        if (utxo.value < amountSats + (feeRate * 200)) // Rough estimate: 200 vB transaction
        {
            throw new InvalidOperationException(
                $"UTXO has insufficient funds. UTXO: {utxo.value} sats, Needed: {amountSats + (feeRate * 200)} sats (including estimated fee)");
        }

        // Create transaction
        var transaction = _network.CreateTransaction();
        
        // Add output to destination address
        var destinationAddress = BitcoinAddress.Create(toAddress, _network);
        transaction.AddOutput(Money.Satoshis(amountSats), destinationAddress.ScriptPubKey);

        // Get change address (use next address from miner wallet)
        var changeAddress = minerAccountInfo.AddressesInfo.First().Address;
        if (string.IsNullOrEmpty(changeAddress))
        {
            throw new InvalidOperationException("Could not get change address from miner wallet");
        }

        // Add inputs and sign transaction
        var signedTransaction = _walletOperations.AddInputsAndSignTransaction(
            changeAddress,
            transaction,
            minerWords,
            minerAccountInfo,
            feeRate);

        _logger.LogInformation("Transaction created: Inputs: {Inputs}, Outputs: {Outputs}, Fee: {Fee} sats",
            signedTransaction.Transaction.Inputs.Count,
            signedTransaction.Transaction.Outputs.Count,
            signedTransaction.TransactionFee);

        // Publish transaction
        var publishResult = await _walletOperations.PublishTransactionAsync(_network, signedTransaction.Transaction);
        
        if (!publishResult.Success)
        {
            throw new InvalidOperationException($"Failed to publish transaction: {publishResult.Message}");
        }

        var txId = signedTransaction.Transaction.GetHash().ToString();
        _logger.LogInformation("✅ Funding transaction published: {TxId}", txId);

        return txId;
    }

    /// <summary>
    /// Waits for a transaction to appear in the mempool or be confirmed.
    /// </summary>
    /// <param name="txId">Transaction ID to monitor</param>
    /// <param name="timeoutSeconds">Maximum time to wait (default: 60 seconds)</param>
    public async Task WaitForTransactionAsync(string txId, int timeoutSeconds = 60)
    {
        _logger.LogInformation("Waiting for transaction {TxId} to be detected...", txId);

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                // Try to fetch the transaction
                var txHex = await _indexerService.GetTransactionHexByIdAsync(txId);
                if (!string.IsNullOrEmpty(txHex))
                {
                    _logger.LogInformation("✅ Transaction {TxId} detected in mempool/blockchain", txId);
                    return;
                }
            }
            catch
            {
                // Transaction not found yet, continue waiting
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"Transaction {txId} not detected after {timeoutSeconds} seconds");
    }

    /// <summary>
    /// Funds an address and waits for the transaction to be detected.
    /// This is a convenience method that combines SendFundsToAddressAsync and WaitForTransactionAsync.
    /// </summary>
    /// <param name="toAddress">Destination address to fund</param>
    /// <param name="amountSats">Amount to send in satoshis</param>
    /// <param name="feeRate">Fee rate in sats/vB (default: 10)</param>
    /// <param name="waitForConfirmation">Whether to wait for the transaction to be detected (default: true)</param>
    /// <returns>Transaction ID of the funding transaction</returns>
    public async Task<string> FundAddressAsync(
        string toAddress, 
        long amountSats, 
        long feeRate = 10,
        bool waitForConfirmation = true)
    {
        var txId = await SendFundsToAddressAsync(toAddress, amountSats, feeRate);

        if (waitForConfirmation)
        {
            await WaitForTransactionAsync(txId);
        }

        return txId;
    }

    /// <summary>
    /// Gets the miner wallet account info.
    /// Useful for advanced scenarios where you need direct access to the miner wallet.
    /// Note: Only generates first 10 addresses to avoid performance issues (miner has thousands).
    /// </summary>
    public Task<AccountInfo> GetMinerAccountInfoAsync()
    {
        var minerWords = new WalletWords { Words = MinerWalletWords, Passphrase = MinerWalletPassphrase };
        var minerAccountInfo = _walletOperations.BuildAccountInfoForWalletWords(minerWords);
        var accountExtPubKey = ExtPubKey.Parse(minerAccountInfo.ExtPubKey, _network);
        
        // Manually generate first 10 addresses (don't use UpdateAccountInfoWithNewAddressesAsync - too slow!)
        for (int i = 0; i < 10; i++)
        {
            var addressInfo = GenerateAddress(accountExtPubKey, i, isChange: false);
            minerAccountInfo.AddressesInfo.Add(addressInfo);
        }
        
        return Task.FromResult(minerAccountInfo);
    }

    /// <summary>
    /// Generates an address from the extended public key.
    /// Replicates the logic from WalletOperations.GenerateAddressFromPubKey (which is private).
    /// </summary>
    private AddressInfo GenerateAddress(ExtPubKey accountExtPubKey, int index, bool isChange)
    {
        var hdOperations = new HdOperations();
        var pubKey = hdOperations.GeneratePublicKey(accountExtPubKey, index, isChange);
        var coinType = _network.Consensus.CoinType;
        var path = hdOperations.CreateHdPath(84, coinType, 0, isChange, index); // Purpose=84 (BIP84), AccountIndex=0
        var address = pubKey.GetSegwitAddress(_network).ToString();

        return new AddressInfo { Address = address, HdPath = path };
    }
}


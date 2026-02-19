using System.Text.Json;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Service for claiming funds from Boltz reverse submarine swaps.
/// Encapsulates all Taproot/MuSig2 claim logic.
/// </summary>
public class BoltzClaimService : IBoltzClaimService
{
    private readonly IBoltzSwapService _boltzSwapService;
    private readonly IIndexerService _indexerService;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly ILogger<BoltzClaimService> _logger;

    public BoltzClaimService(
        IBoltzSwapService boltzSwapService,
        IIndexerService indexerService,
        INetworkConfiguration networkConfiguration,
        ILogger<BoltzClaimService> logger)
    {
        _boltzSwapService = boltzSwapService;
        _indexerService = indexerService;
        _networkConfiguration = networkConfiguration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<BoltzClaimResult>> ClaimSwapAsync(
        BoltzSubmarineSwap swap,
        string claimPrivateKeyHex,
        string lockupTransactionHex,
        int lockupOutputIndex = 0,
        long feeRate = 2)
    {
        try
        {
            _logger.LogInformation(
                "Claiming funds from swap {SwapId}. LockupAddress: {LockupAddress}, DestAddress: {DestAddress}",
                swap.Id, swap.LockupAddress, swap.Address);

            // Validate we have all required data
            if (string.IsNullOrEmpty(swap.Preimage))
            {
                return Result.Failure<BoltzClaimResult>("Swap preimage is missing - cannot claim without it");
            }

            // Verify the preimage matches the expected hash
            var preimageBytes = Convert.FromHexString(swap.Preimage);
            var computedHash = System.Security.Cryptography.SHA256.HashData(preimageBytes);
            var computedHashHex = Convert.ToHexString(computedHash).ToLowerInvariant();
            _logger.LogDebug("Preimage hash verification - Computed: {ComputedHash}, Expected: {ExpectedHash}", 
                computedHashHex, swap.PreimageHash?.ToLowerInvariant() ?? "unknown");
            
            if (!string.IsNullOrEmpty(swap.PreimageHash) && 
                !computedHashHex.Equals(swap.PreimageHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Preimage hash mismatch! This may cause claiming to fail.");
            }

            // STEP 1: Try cooperative claiming via Boltz API using MuSig2
            _logger.LogInformation("Attempting cooperative MuSig2 claim via Boltz API...");
            try
            {
                var cooperativeResult = await CooperativeMusig2Claim(
                    swap, claimPrivateKeyHex, lockupTransactionHex, lockupOutputIndex, feeRate, preimageBytes);
                
                if (cooperativeResult.IsSuccess)
                {
                    _logger.LogInformation("Cooperative MuSig2 claim succeeded!");
                    return cooperativeResult;
                }
                
                _logger.LogWarning("Cooperative MuSig2 claim failed: {Error}. Will try script path...", 
                    cooperativeResult.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cooperative MuSig2 claim threw exception. Will try script path...");
            }

            // STEP 2: Manual claiming - build and broadcast our own transaction
            _logger.LogInformation("Proceeding with manual claim transaction (Taproot script path spend)...");
            
            return await BuildAndBroadcastClaimTransaction(
                swap, claimPrivateKeyHex, lockupTransactionHex, lockupOutputIndex, feeRate, preimageBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming swap {SwapId}", swap.Id);
            return Result.Failure<BoltzClaimResult>($"Error claiming swap: {ex.Message}");
        }
    }

    /// <summary>
    /// Cooperative MuSig2 claim - the preferred method for claiming Boltz swaps.
    /// </summary>
    private async Task<Result<BoltzClaimResult>> CooperativeMusig2Claim(
        BoltzSubmarineSwap swap,
        string claimPrivateKeyHex,
        string lockupTransactionHex,
        int lockupOutputIndex,
        long feeRate,
        byte[] preimageBytes)
    {
        _logger.LogInformation("Starting MuSig2 cooperative claim for swap {SwapId}", swap.Id);
        
        if (string.IsNullOrEmpty(swap.RefundPublicKey))
        {
            return Result.Failure<BoltzClaimResult>("Boltz refund public key is missing - required for MuSig2");
        }
        
        if (string.IsNullOrEmpty(swap.SwapTree))
        {
            return Result.Failure<BoltzClaimResult>("Swap tree is missing - required for Taproot tweak computation");
        }
        
        var network = GetNBitcoinNetwork();
        var lockupTx = NBitcoin.Transaction.Parse(lockupTransactionHex, network);
        
        // Find the lockup output
        NBitcoin.TxOut? lockupOutput = null;
        int foundOutputIndex = lockupOutputIndex;
        
        for (int i = 0; i < lockupTx.Outputs.Count; i++)
        {
            var output = lockupTx.Outputs[i];
            var outputAddress = output.ScriptPubKey.GetDestinationAddress(network)?.ToString();
            if (outputAddress == swap.LockupAddress)
            {
                foundOutputIndex = i;
                lockupOutput = output;
                break;
            }
        }
        
        if (lockupOutput == null)
        {
            lockupOutput = lockupTx.Outputs[lockupOutputIndex];
        }
        
        var lockupOutpoint = new NBitcoin.OutPoint(lockupTx.GetHash(), foundOutputIndex);
        
        // Initialize MuSig2 session
        var claimPrivateKeyBytes = Convert.FromHexString(claimPrivateKeyHex);
        var boltzRefundKeyBytes = Convert.FromHexString(swap.RefundPublicKey);
        
        // Verify the derived claim key matches what was registered with Boltz
        if (!string.IsNullOrEmpty(swap.ClaimPublicKey))
        {
            var derivedPubKeyBytes = NBitcoin.Secp256k1.ECPrivKey.Create(claimPrivateKeyBytes).CreatePubKey().ToBytes();
            var derivedPubKeyHex = Convert.ToHexString(derivedPubKeyBytes).ToLowerInvariant();
            var storedClaimKey = swap.ClaimPublicKey.ToLowerInvariant();
            
            var derivedXOnly = derivedPubKeyHex.Length == 66 ? derivedPubKeyHex.Substring(2) : derivedPubKeyHex;
            var storedXOnly = storedClaimKey.Length == 66 ? storedClaimKey.Substring(2) : storedClaimKey;
            
            if (derivedXOnly != storedXOnly)
            {
                _logger.LogError(
                    "CRITICAL: Derived claim key does not match stored claim key! Derived: {Derived}, Stored: {Stored}",
                    derivedPubKeyHex, storedClaimKey);
                return Result.Failure<BoltzClaimResult>(
                    $"Claim key mismatch - derived key doesn't match registered key. Wallet or project data may have changed.");
            }
            
            _logger.LogDebug("Derived claim key matches stored key: {Key}", derivedPubKeyHex);
        }
        
        var musig = new BoltzMusig2(claimPrivateKeyBytes, boltzRefundKeyBytes, _logger);
        
        // Parse the swap tree
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var swapTree = JsonSerializer.Deserialize<SwapTreeDto>(swap.SwapTree, jsonOptions);
        
        if (swapTree?.ClaimLeaf?.Output == null || swapTree?.RefundLeaf?.Output == null)
        {
            return Result.Failure<BoltzClaimResult>(
                $"Invalid swap tree - missing claim or refund leaf. SwapTree: {swap.SwapTree}");
        }
        
        // Compute merkle root and apply Taproot tweak
        var merkleRoot = ComputeSwapTreeMerkleRoot(swapTree.ClaimLeaf.Output, swapTree.RefundLeaf.Output);
        _logger.LogDebug("Computed swap tree merkle root: {MerkleRoot}", Convert.ToHexString(merkleRoot));
        
        musig.SetTaprootTweak(merkleRoot);
        
        // Verify the tweaked output key matches the lockup address
        var outputKeyXOnly = musig.GetOutputPubKeyXOnly();
        var outputKey = new NBitcoin.TaprootPubKey(outputKeyXOnly);
        var computedAddress = outputKey.GetAddress(network).ToString();
        
        if (computedAddress != swap.LockupAddress)
        {
            _logger.LogError(
                "CRITICAL: Tweaked output key address mismatch! Computed: {Computed}, Expected: {Expected}",
                computedAddress, swap.LockupAddress);
            return Result.Failure<BoltzClaimResult>(
                $"Address mismatch - computed lockup address ({computedAddress}) doesn't match expected ({swap.LockupAddress})");
        }
        
        _logger.LogInformation("Tweaked output key matches lockup address: {Address}", computedAddress);
        
        // Build the claim transaction
        var destAddress = NBitcoin.BitcoinAddress.Create(swap.Address, network);
        var estimatedVbytes = 110;
        var fee = feeRate * estimatedVbytes;
        var outputAmount = lockupOutput.Value.Satoshi - fee;
        
        if (outputAmount <= 546)
        {
            return Result.Failure<BoltzClaimResult>($"Output amount after fee is below dust threshold");
        }
        
        var claimTx = NBitcoin.Transaction.Create(network);
        claimTx.Version = 2;
        claimTx.Inputs.Add(new NBitcoin.TxIn(lockupOutpoint));
        claimTx.Inputs[0].Sequence = 0xFFFFFFFD;
        claimTx.Outputs.Add(new NBitcoin.TxOut(NBitcoin.Money.Satoshis(outputAmount), destAddress.ScriptPubKey));
        
        // Generate our nonce
        var ourPubNonce = musig.GenerateNonce();
        var ourPubNonceHex = Convert.ToHexString(ourPubNonce).ToLowerInvariant();
        
        // Send the claim request to Boltz
        var claimTxHex = claimTx.ToHex();
        var preimageHex = Convert.ToHexString(preimageBytes).ToLowerInvariant();
        
        _logger.LogInformation("Sending cooperative claim request to Boltz API...");
        var claimResponse = await _boltzSwapService.GetClaimSignatureAsync(
            swap.Id, claimTxHex, preimageHex, ourPubNonceHex);
        
        if (claimResponse.IsFailure)
        {
            return Result.Failure<BoltzClaimResult>($"Boltz claim API failed: {claimResponse.Error}");
        }
        
        var boltzResponse = claimResponse.Value;
        
        if (string.IsNullOrEmpty(boltzResponse.PubNonce) || string.IsNullOrEmpty(boltzResponse.PartialSignature))
        {
            return Result.Failure<BoltzClaimResult>("Boltz returned empty nonce or signature");
        }
        
        // Aggregate nonces and sign
        var boltzNonceBytes = Convert.FromHexString(boltzResponse.PubNonce);
        musig.AggregateNonces(boltzNonceBytes);
        
        var prevOuts = new NBitcoin.TxOut[] { lockupOutput };
        var sighash = claimTx.GetSignatureHashTaproot(prevOuts, 
            new NBitcoin.TaprootExecutionData(0) { SigHash = NBitcoin.TaprootSigHash.Default });
        
        musig.InitializeSession(sighash.ToBytes());
        var ourPartialSig = musig.SignPartial();
        
        var boltzPartialSig = Convert.FromHexString(boltzResponse.PartialSignature);
        var aggregatedSig = musig.AggregatePartials(boltzPartialSig, ourPartialSig);
        
        // Verify the aggregated signature
        var outputPubKeyForVerify = new NBitcoin.TaprootPubKey(musig.GetOutputPubKeyXOnly());
        var schnorrSig = new NBitcoin.Crypto.SchnorrSignature(aggregatedSig);
        if (!outputPubKeyForVerify.VerifySignature(sighash, schnorrSig))
        {
            _logger.LogError("Schnorr signature verification FAILED before broadcast!");
            return Result.Failure<BoltzClaimResult>("Aggregated Schnorr signature failed verification");
        }
        _logger.LogInformation("Schnorr signature verification PASSED");
        
        // Set the witness and broadcast
        claimTx.Inputs[0].WitScript = new NBitcoin.WitScript(new[] { aggregatedSig });
        
        var signedTxHex = claimTx.ToHex();
        var claimTxId = claimTx.GetHash().ToString();
        
        _logger.LogInformation("Built cooperative claim transaction: {TxId}", claimTxId);
        
        var broadcastResult = await _boltzSwapService.BroadcastTransactionAsync(signedTxHex);
        
        if (broadcastResult.IsFailure)
        {
            _logger.LogWarning("Boltz broadcast failed: {Error}. Trying indexer...", broadcastResult.Error);
            var indexerError = await _indexerService.PublishTransactionAsync(signedTxHex);
            
            if (!string.IsNullOrEmpty(indexerError))
            {
                return Result.Failure<BoltzClaimResult>(
                    $"Failed to broadcast: Boltz: {broadcastResult.Error}, Indexer: {indexerError}");
            }
        }
        
        _logger.LogInformation("Successfully claimed swap {SwapId} via MuSig2. TxId: {TxId}", swap.Id, claimTxId);
        
        return Result.Success(new BoltzClaimResult(claimTxId, signedTxHex));
    }

    /// <summary>
    /// Script path fallback for claiming when MuSig2 fails.
    /// </summary>
    private async Task<Result<BoltzClaimResult>> BuildAndBroadcastClaimTransaction(
        BoltzSubmarineSwap swap,
        string claimPrivateKeyHex,
        string lockupTransactionHex,
        int lockupOutputIndex,
        long feeRate,
        byte[] preimageBytes)
    {
        try
        {
            if (string.IsNullOrEmpty(swap.SwapTree))
            {
                _logger.LogWarning("Swap tree is missing from storage, fetching from Boltz API...");
                var swapDetailsResult = await _boltzSwapService.GetSwapDetailsAsync(swap.Id);
                if (swapDetailsResult.IsSuccess && !string.IsNullOrEmpty(swapDetailsResult.Value.SwapTree))
                {
                    swap.SwapTree = swapDetailsResult.Value.SwapTree;
                    _logger.LogInformation("Retrieved swap tree from API");
                }
                else
                {
                    var errorMsg = swapDetailsResult.IsFailure ? swapDetailsResult.Error : "SwapTree was empty";
                    return Result.Failure<BoltzClaimResult>($"Swap tree is missing and could not be fetched: {errorMsg}");
                }
            }

            if (string.IsNullOrEmpty(claimPrivateKeyHex))
            {
                return Result.Failure<BoltzClaimResult>("Claim private key is required");
            }

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var swapTree = JsonSerializer.Deserialize<SwapTreeDto>(swap.SwapTree, jsonOptions);
            
            if (swapTree?.ClaimLeaf?.Output == null)
            {
                return Result.Failure<BoltzClaimResult>($"Invalid swap tree - missing claim leaf");
            }

            var network = GetNBitcoinNetwork();
            var lockupTx = NBitcoin.Transaction.Parse(lockupTransactionHex, network);
            
            // Find the lockup output
            int foundOutputIndex = -1;
            NBitcoin.TxOut? lockupOutput = null;
            
            for (int i = 0; i < lockupTx.Outputs.Count; i++)
            {
                var output = lockupTx.Outputs[i];
                var outputAddress = output.ScriptPubKey.GetDestinationAddress(network)?.ToString();
                if (outputAddress == swap.LockupAddress)
                {
                    foundOutputIndex = i;
                    lockupOutput = output;
                    _logger.LogInformation("Found lockup output at index {Index}", i);
                    break;
                }
            }

            if (foundOutputIndex == -1)
            {
                foundOutputIndex = lockupOutputIndex;
                lockupOutput = lockupTx.Outputs[lockupOutputIndex];
            }
            
            if (lockupOutput == null)
            {
                return Result.Failure<BoltzClaimResult>("Could not find lockup output in transaction");
            }
            
            var lockupOutpoint = new NBitcoin.OutPoint(lockupTx.GetHash(), foundOutputIndex);

            // Check if UTXO is spent
            try
            {
                var txId = lockupTx.GetHash().ToString();
                var spentOutputs = await _indexerService.GetIsSpentOutputsOnTransactionAsync(txId);
                var outputSpentInfo = spentOutputs.FirstOrDefault(o => o.index == foundOutputIndex);
                if (outputSpentInfo.spent)
                {
                    _logger.LogError("UTXO at index {Index} is already spent!", foundOutputIndex);
                    return Result.Failure<BoltzClaimResult>("The lockup UTXO has already been spent.");
                }
                _logger.LogInformation("UTXO verified as unspent - proceeding with claim");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify UTXO spent status - proceeding anyway");
            }

            // Parse claim script
            var claimScriptHex = swapTree.ClaimLeaf.Output;
            var claimScript = new NBitcoin.Script(Convert.FromHexString(claimScriptHex));

            // Build claim transaction
            var claimTx = BuildClaimTransaction(lockupOutpoint, lockupOutput, swap.Address, feeRate, network);

            // Sign the transaction
            var claimKey = new NBitcoin.Key(Convert.FromHexString(claimPrivateKeyHex));
            var tapScript = claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0);
            var leafHash = tapScript.LeafHash;

            var prevOuts = new NBitcoin.TxOut[] { lockupOutput };
            var execData = new NBitcoin.TaprootExecutionData(0, leafHash) { SigHash = NBitcoin.TaprootSigHash.Default };
            var sighash = claimTx.GetSignatureHashTaproot(prevOuts, execData);
            
            var signature = claimKey.SignTaprootKeySpend(sighash, NBitcoin.TaprootSigHash.Default);

            // Build the control block
            var (controlBlock, computedTaprootAddress) = BuildControlBlockWithVerification(
                swapTree, claimScript, swap.ClaimPublicKey, swap.RefundPublicKey, network);
            
            if (computedTaprootAddress != swap.LockupAddress)
            {
                _logger.LogWarning("Address mismatch! Computed: {Computed}, Expected: {Expected}",
                    computedTaprootAddress, swap.LockupAddress);
            }

            // Build witness
            var witness = new NBitcoin.WitScript(new[] {
                signature.ToBytes(),
                preimageBytes,
                claimScript.ToBytes(),
                controlBlock
            });
            claimTx.Inputs[0].WitScript = witness;

            var signedClaimHex = claimTx.ToHex();
            var claimTxId = claimTx.GetHash().ToString();
            
            _logger.LogInformation("Built signed claim transaction: {TxId}", claimTxId);

            // Broadcast
            var broadcastResult = await _boltzSwapService.BroadcastTransactionAsync(signedClaimHex);
            
            if (broadcastResult.IsFailure)
            {
                _logger.LogWarning("Boltz broadcast failed: {Error}. Trying indexer...", broadcastResult.Error);
                var indexerError = await _indexerService.PublishTransactionAsync(signedClaimHex);
                
                if (!string.IsNullOrEmpty(indexerError))
                {
                    return Result.Failure<BoltzClaimResult>(
                        $"Failed to broadcast via Boltz: {broadcastResult.Error}. Indexer: {indexerError}");
                }
                _logger.LogInformation("Transaction broadcast successfully via indexer");
            }
            else
            {
                _logger.LogInformation("Transaction broadcast successfully via Boltz: {TxId}", broadcastResult.Value);
            }

            _logger.LogInformation("Successfully claimed swap {SwapId}. Claim TxId: {TxId}", swap.Id, claimTxId);

            return Result.Success(new BoltzClaimResult(claimTxId, signedClaimHex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming swap {SwapId}", swap.Id);
            return Result.Failure<BoltzClaimResult>($"Error claiming swap: {ex.Message}");
        }
    }

    private NBitcoin.Network GetNBitcoinNetwork()
    {
        var blockcoreNetwork = _networkConfiguration.GetNetwork();
        var networkName = blockcoreNetwork.Name.ToLowerInvariant();
        
        if (networkName.Contains("main"))
            return NBitcoin.Network.Main;
        if (networkName.Contains("testnet") || networkName.Contains("test"))
            return NBitcoin.Network.TestNet;
        if (networkName.Contains("signet"))
            return NBitcoin.Network.GetNetwork("signet") ?? NBitcoin.Network.TestNet;
        if (networkName.Contains("regtest"))
            return NBitcoin.Network.RegTest;
        
        return NBitcoin.Network.TestNet;
    }

    private NBitcoin.Transaction BuildClaimTransaction(
        NBitcoin.OutPoint lockupOutpoint,
        NBitcoin.TxOut lockupOutput,
        string destinationAddress,
        long feeRate,
        NBitcoin.Network network)
    {
        var destAddress = NBitcoin.BitcoinAddress.Create(destinationAddress, network);
        var estimatedVbytes = 200;
        var fee = feeRate * estimatedVbytes;
        var outputAmount = lockupOutput.Value.Satoshi - fee;

        if (outputAmount <= 546)
        {
            throw new InvalidOperationException(
                $"Output amount ({lockupOutput.Value.Satoshi} - {fee} = {outputAmount}) is below dust threshold");
        }

        var claimTx = NBitcoin.Transaction.Create(network);
        claimTx.Version = 2;
        claimTx.Inputs.Add(new NBitcoin.TxIn(lockupOutpoint));
        claimTx.Inputs[0].Sequence = 0xFFFFFFFD;
        claimTx.Outputs.Add(new NBitcoin.TxOut(NBitcoin.Money.Satoshis(outputAmount), destAddress.ScriptPubKey));

        return claimTx;
    }

    private (byte[] ControlBlock, string ComputedAddress) BuildControlBlockWithVerification(
        SwapTreeDto swapTree, 
        NBitcoin.Script claimScript, 
        string claimPublicKeyHex, 
        string refundPublicKeyHex,
        NBitcoin.Network network)
    {
        var refundScriptHex = swapTree.RefundLeaf?.Output ?? "";
        var refundScript = new NBitcoin.Script(Convert.FromHexString(refundScriptHex));

        NBitcoin.TaprootInternalPubKey internalKey;
        
        try
        {
            var claimPubKeyBytes = Convert.FromHexString(claimPublicKeyHex.Length == 66 
                ? claimPublicKeyHex.Substring(2)
                : claimPublicKeyHex);
            var refundPubKeyBytes = Convert.FromHexString(refundPublicKeyHex.Length == 66 
                ? refundPublicKeyHex.Substring(2)
                : refundPublicKeyHex);
            
            var aggregateXOnly = BoltzMusig2.KeyAggSorted(claimPubKeyBytes, refundPubKeyBytes);
            internalKey = new NBitcoin.TaprootInternalPubKey(aggregateXOnly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute MuSig2 internal key, falling back to unspendable key");
            internalKey = NBitcoin.TaprootInternalPubKey.Parse(
                "50929b74c1a04954b78b4b6035e97a5e078a5a0f28ec96d547bfee9ace803ac0");
        }

        var builder1 = new NBitcoin.TaprootBuilder();
        builder1.AddLeaf(1, claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0));
        builder1.AddLeaf(1, refundScript.ToTapScript(NBitcoin.TapLeafVersion.C0));
        var spendInfo = builder1.Finalize(internalKey);
        var computedAddress = spendInfo.OutputPubKey.GetAddress(network).ToString();

        var controlBlock = spendInfo.GetControlBlock(claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0));
        
        if (controlBlock == null)
        {
            throw new InvalidOperationException("Failed to get control block for claim script");
        }

        return (controlBlock.ToBytes(), computedAddress);
    }

    private static byte[] ComputeSwapTreeMerkleRoot(string claimScriptHex, string refundScriptHex)
    {
        var claimScript = new NBitcoin.Script(Convert.FromHexString(claimScriptHex));
        var refundScript = new NBitcoin.Script(Convert.FromHexString(refundScriptHex));

        var claimLeafHash = claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0).LeafHash;
        var refundLeafHash = refundScript.ToTapScript(NBitcoin.TapLeafVersion.C0).LeafHash;

        var claimHashBytes = claimLeafHash.ToBytes();
        var refundHashBytes = refundLeafHash.ToBytes();

        byte[] left, right;
        if (CompareBytesLex(claimHashBytes, refundHashBytes) <= 0)
        {
            left = claimHashBytes;
            right = refundHashBytes;
        }
        else
        {
            left = refundHashBytes;
            right = claimHashBytes;
        }

        var tagBytes = System.Text.Encoding.UTF8.GetBytes("TapBranch");
        var tagHash = System.Security.Cryptography.SHA256.HashData(tagBytes);
        var data = new byte[tagHash.Length * 2 + 32 + 32];
        var offset = 0;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(left, 0, data, offset, 32); offset += 32;
        Array.Copy(right, 0, data, offset, 32);

        return System.Security.Cryptography.SHA256.HashData(data);
    }

    private static int CompareBytesLex(byte[] a, byte[] b)
    {
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        }
        return a.Length.CompareTo(b.Length);
    }

    // DTO for deserializing the swap tree
    private class SwapTreeDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("claimLeaf")]
        public SwapLeafDto? ClaimLeaf { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("refundLeaf")]
        public SwapLeafDto? RefundLeaf { get; set; }
    }

    private class SwapLeafDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("version")]
        public int Version { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;
    }
}


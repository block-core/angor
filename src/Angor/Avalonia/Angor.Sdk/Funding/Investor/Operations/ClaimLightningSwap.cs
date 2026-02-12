using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Shared;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
// Use fully qualified names for NBitcoin types to avoid conflicts with Blockcore.NBitcoin

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Claims funds from a Boltz reverse submarine swap.
/// 
/// For reverse submarine swaps, funds are locked in a Taproot output with two spend paths:
/// 1. Claim path (cooperative): preimage + signature (what we use)
/// 2. Refund path (timeout): Boltz can refund after timeout
/// 
/// This operation builds and broadcasts the claim transaction using the preimage
/// and our claim key to spend from the lockup address to our destination.
/// </summary>
public static class ClaimLightningSwap
{
    /// <summary>
    /// Request to claim funds from a completed Lightning swap using stored swap data.
    /// This is the preferred request type - it retrieves swap data from storage.
    /// </summary>
    /// <param name="WalletId">The wallet ID that created the swap</param>
    /// <param name="SwapId">The Boltz swap ID to claim</param>
    /// <param name="LockupTransactionHex">The hex of the lockup transaction (optional if stored)</param>
    /// <param name="LockupOutputIndex">The output index in the lockup transaction (usually 0)</param>
    /// <param name="FeeRate">Fee rate in sat/vbyte for the claim transaction</param>
    public record ClaimLightningSwapByIdRequest(
        WalletId WalletId,
        string SwapId,
        string? LockupTransactionHex = null,
        int LockupOutputIndex = 0,
        long FeeRate = 2) : IRequest<Result<ClaimLightningSwapResponse>>;



    /// <summary>
    /// Response containing the broadcast claim transaction
    /// </summary>
    /// <param name="ClaimTransactionId">Transaction ID of the broadcast claim transaction</param>
    /// <param name="ClaimTransactionHex">Hex of the signed claim transaction</param>
    public record ClaimLightningSwapResponse(
        string ClaimTransactionId,
        string ClaimTransactionHex);

    /// <summary>
    /// Handler for ClaimLightningSwapByIdRequest - retrieves swap from storage and claims funds
    /// </summary>
    public class ClaimLightningSwapByIdHandler(
        IBoltzSwapService boltzSwapService,
        IBoltzSwapStorageService swapStorageService,
        INetworkConfiguration networkConfiguration,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        ILogger<ClaimLightningSwapByIdHandler> logger)
        : IRequestHandler<ClaimLightningSwapByIdRequest, Result<ClaimLightningSwapResponse>>
    {
        public async Task<Result<ClaimLightningSwapResponse>> Handle(
            ClaimLightningSwapByIdRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Claiming swap {SwapId} for wallet {WalletId}", 
                    request.SwapId, request.WalletId.Value);

                // Step 1: Get the swap from storage
                var swapResult = await swapStorageService.GetSwapAsync(request.SwapId);
                if (swapResult.IsFailure || swapResult.Value == null)
                {
                    return Result.Failure<ClaimLightningSwapResponse>(
                        $"Swap not found in storage: {request.SwapId}");
                }

                var swapDoc = swapResult.Value;
                var swap = swapDoc.ToSwapModel();

                // Step 2: Derive the claim private key
                var privateKeyResult = await DeriveClaimPrivateKey(request.WalletId, swapDoc.ProjectId);
                if (privateKeyResult.IsFailure)
                {
                    return Result.Failure<ClaimLightningSwapResponse>(privateKeyResult.Error);
                }

                // Step 3: Get lockup transaction hex
                var lockupTxHex = request.LockupTransactionHex ?? swapDoc.LockupTransactionHex;
                if (string.IsNullOrEmpty(lockupTxHex))
                {
                    return Result.Failure<ClaimLightningSwapResponse>(
                        "Lockup transaction hex not available. Please provide it or fetch from a block explorer.");
                }

                // Step 4: Claim the swap using the private method
                return await ClaimSwapAsync(
                    swap,
                    privateKeyResult.Value,
                    lockupTxHex,
                    request.LockupOutputIndex,
                    request.FeeRate);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error claiming swap {SwapId}", request.SwapId);
                return Result.Failure<ClaimLightningSwapResponse>($"Error claiming swap: {ex.Message}");
            }
        }

        private async Task<Result<string>> DeriveClaimPrivateKey(WalletId walletId, string? projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                return Result.Failure<string>("Project ID is required to derive claim key");
            }

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId.Value);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<string>($"Failed to get wallet data: {sensitiveDataResult.Error}");
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();

            // The claim key was derived using the founder key from the project
            Blockcore.NBitcoin.Key claimPrivateKey = derivationOperations.DeriveInvestorPrivateKey(walletWords, projectId);
            
            // Get the private key bytes and convert to hex using Blockcore's encoder
            var privateKeyHex = Encoders.Hex.EncodeData(claimPrivateKey.ToBytes());
            return Result.Success(privateKeyHex);
        }

        /// <summary>
        /// Claims funds from a completed Lightning swap.
        /// This is the core claim logic used internally.
        /// </summary>
        private async Task<Result<ClaimLightningSwapResponse>> ClaimSwapAsync(
            BoltzSubmarineSwap swap,
            string claimPrivateKeyHex,
            string lockupTransactionHex,
            int lockupOutputIndex,
            long feeRate)
        {
            try
            {
                logger.LogInformation(
                    "Claiming funds from swap {SwapId}. LockupAddress: {LockupAddress}, DestAddress: {DestAddress}",
                    swap.Id, swap.LockupAddress, swap.Address);

                // Validate we have all required data
                if (string.IsNullOrEmpty(swap.Preimage))
                {
                    return Result.Failure<ClaimLightningSwapResponse>("Swap preimage is missing - cannot claim without it");
                }

                if (string.IsNullOrEmpty(swap.SwapTree))
                {
                    return Result.Failure<ClaimLightningSwapResponse>("Swap tree is missing - cannot determine claim script");
                }

                if (string.IsNullOrEmpty(claimPrivateKeyHex))
                {
                    return Result.Failure<ClaimLightningSwapResponse>("Claim private key is required");
                }

                // Parse the swap tree to get the claim script
                var swapTree = JsonSerializer.Deserialize<SwapTreeDto>(swap.SwapTree);
                if (swapTree?.ClaimLeaf?.Output == null)
                {
                    return Result.Failure<ClaimLightningSwapResponse>("Invalid swap tree - missing claim leaf");
                }

                // Step 1: Parse the lockup transaction and extract the output
                var network = GetNBitcoinNetwork();
                var lockupTx = NBitcoin.Transaction.Parse(lockupTransactionHex, network);
                var lockupOutput = lockupTx.Outputs[lockupOutputIndex];
                var lockupOutpoint = new NBitcoin.OutPoint(lockupTx.GetHash(), lockupOutputIndex);

                logger.LogDebug(
                    "Lockup output: {Amount} sats at index {Index}, script: {Script}",
                    lockupOutput.Value.Satoshi, lockupOutputIndex, lockupOutput.ScriptPubKey);

                // Step 2: Parse the claim script from swap tree
                var claimScriptHex = swapTree.ClaimLeaf.Output;
                var claimScript = new NBitcoin.Script(Convert.FromHexString(claimScriptHex));
                
                logger.LogDebug("Claim script: {Script}", claimScript);

                // Step 3: Build the unsigned claim transaction
                var claimTx = BuildClaimTransaction(
                    lockupOutpoint,
                    lockupOutput,
                    swap.Address,
                    feeRate,
                    network);

                logger.LogDebug("Built unsigned claim transaction");

                // Step 4: Sign the transaction with our claim key
                var claimKey = new NBitcoin.Key(Convert.FromHexString(claimPrivateKeyHex));
                var preimageBytes = Convert.FromHexString(swap.Preimage);

                // For Taproot script path spend, we need:
                // 1. Compute the signature for the claim script
                // 2. Build the witness with: <signature> <preimage> <claim_script> <control_block>

                // Get the leaf hash for the claim script
                var tapScript = claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0);
                var leafHash = tapScript.LeafHash;

                // Sign the transaction
                var prevOuts = new NBitcoin.TxOut[] { lockupOutput };
                var execData = new NBitcoin.TaprootExecutionData(0, leafHash) { SigHash = NBitcoin.TaprootSigHash.Default };
                var sighash = claimTx.GetSignatureHashTaproot(prevOuts, execData);
                var signature = claimKey.SignTaprootKeySpend(sighash, NBitcoin.TaprootSigHash.Default);

                logger.LogDebug("Signed claim transaction");

                // Build the control block
                // The control block proves the script is part of the Taproot tree
                // For Boltz, we need to reconstruct the tree from the swap tree data
                var controlBlock = BuildControlBlock(swapTree, claimScript);

                // Build the witness: <signature> <preimage> <claim_script> <control_block>
                var witness = new NBitcoin.WitScript(new[] {
                    signature.ToBytes(),
                    preimageBytes,
                    claimScript.ToBytes(),
                    controlBlock
                });
                claimTx.Inputs[0].WitScript = witness;

                var signedClaimHex = claimTx.ToHex();
                var claimTxId = claimTx.GetHash().ToString();
                
                logger.LogInformation("Built signed claim transaction: {TxId}", claimTxId);

                // Step 5: Broadcast the claim transaction
                var broadcastResult = await boltzSwapService.BroadcastTransactionAsync(signedClaimHex);

                if (broadcastResult.IsFailure)
                {
                    logger.LogError("Failed to broadcast claim transaction: {Error}", broadcastResult.Error);
                    
                    // Try to provide more details
                    logger.LogDebug("Claim TX hex: {TxHex}", signedClaimHex);
                    
                    return Result.Failure<ClaimLightningSwapResponse>("Failed to broadcast: " + broadcastResult.Error);
                }

                // Step 6: Mark swap as claimed in the database
                await swapStorageService.MarkSwapClaimedAsync(swap.Id, broadcastResult.Value);

                logger.LogInformation(
                    "Successfully claimed swap {SwapId}. Claim TxId: {TxId}",
                    swap.Id, broadcastResult.Value);

                return Result.Success(new ClaimLightningSwapResponse(
                    broadcastResult.Value,
                    signedClaimHex));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error claiming swap {SwapId}", swap.Id);
                return Result.Failure<ClaimLightningSwapResponse>($"Error claiming swap: {ex.Message}");
            }
        }

        private NBitcoin.Network GetNBitcoinNetwork()
        {
            var blockcoreNetwork = networkConfiguration.GetNetwork();
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
            // Parse destination address
            var destAddress = NBitcoin.BitcoinAddress.Create(destinationAddress, network);

            // Calculate fee (estimate ~150 vbytes for a Taproot script path spend)
            var estimatedVbytes = 200; // Script path spends are slightly larger
            var fee = feeRate * estimatedVbytes;
            var outputAmount = lockupOutput.Value.Satoshi - fee;

            if (outputAmount <= 546) // Dust threshold
            {
                throw new InvalidOperationException(
                    $"Output amount ({lockupOutput.Value.Satoshi} - {fee} = {outputAmount}) is below dust threshold");
            }

            // Build the claim transaction
            var claimTx = NBitcoin.Transaction.Create(network);
            claimTx.Version = 2;
            
            // Add input (spending from lockup address)
            claimTx.Inputs.Add(new NBitcoin.TxIn(lockupOutpoint));
            claimTx.Inputs[0].Sequence = 0xFFFFFFFD; // Enable RBF
            
            // Add output (sending to destination)
            claimTx.Outputs.Add(new NBitcoin.TxOut(NBitcoin.Money.Satoshis(outputAmount), destAddress.ScriptPubKey));

            return claimTx;
        }

        private byte[] BuildControlBlock(SwapTreeDto swapTree, NBitcoin.Script claimScript)
        {
            // The control block for a Taproot script path spend consists of:
            // - 1 byte: leaf version and parity of output key
            // - 32 bytes: internal public key
            // - 32 * n bytes: merkle proof (path from leaf to root)

            // For Boltz swaps, we need to compute this from the swap tree
            // The refund leaf is the sibling of the claim leaf

            var claimLeafHash = claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0).LeafHash;
            
            var refundScriptHex = swapTree.RefundLeaf?.Output ?? "";
            var refundScript = new NBitcoin.Script(Convert.FromHexString(refundScriptHex));
            var refundLeafHash = refundScript.ToTapScript(NBitcoin.TapLeafVersion.C0).LeafHash;

            // Create the Taproot tree with both leaves
            var builder = new NBitcoin.TaprootBuilder();
            builder.AddLeaf(1, claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0));
            builder.AddLeaf(1, refundScript.ToTapScript(NBitcoin.TapLeafVersion.C0));

            // Use an unspendable internal key (standard for Boltz)
            var internalKey = NBitcoin.TaprootInternalPubKey.Parse(
                "0250929b74c1a04954b78b4b6035e97a5e078a5a0f28ec96d547bfee9ace803ac0");

            var spendInfo = builder.Finalize(internalKey);

            // Get the control block for the claim script
            var controlBlock = spendInfo.GetControlBlock(claimScript.ToTapScript(NBitcoin.TapLeafVersion.C0));

            return controlBlock.ToBytes();
        }

        // DTO for deserializing the swap tree
        private class SwapTreeDto
        {
            public SwapLeafDto? ClaimLeaf { get; set; }
            public SwapLeafDto? RefundLeaf { get; set; }
        }

        private class SwapLeafDto
        {
            public int Version { get; set; }
            public string Output { get; set; } = string.Empty;
        }
    }
}




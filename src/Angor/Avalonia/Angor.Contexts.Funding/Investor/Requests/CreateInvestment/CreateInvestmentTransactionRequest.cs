using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investment.Commands.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using SignRecoveryRequest = Angor.Contexts.Funding.Investment.Commands.CreateInvestment.SignRecoveryRequest;

namespace Angor.Contexts.Funding.Investor.Requests.CreateInvestment;

public class CreateInvestmentTransactionRequest(
    IProjectRepository projectRepository,
    IInvestorTransactionActions investorTransactionActions,
    ISeedwordsProvider seedwordsProvider,
    IWalletOperations walletOperations,
    IDerivationOperations derivationOperations,
    IEncryptionService encryptionService,
    ISerializer serializer,
    IRelayService relayService)
{
    public async Task<Result<PendingInvestment>> Execute(Guid walletId, ProjectId projectId, Amount amount)
    {
        try
        {
            // Get the project and investor key
            var projectResult = await projectRepository.Get(projectId);
            if (projectResult.IsFailure)
                return Result.Failure<PendingInvestment>(projectResult.Error);

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);

            var walletWords = sensitiveDataResult.Value.ToWalletWords();

            var investorKey = derivationOperations.DeriveInvestorKey(walletWords, projectResult.Value.FounderKey);

            if (sensitiveDataResult.IsFailure)
                return Result.Failure<PendingInvestment>(sensitiveDataResult.Error);

            var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(
                projectResult.Value.ToSharedModel(),
                investorKey,
                amount.Sats));

            if (transactionResult.IsFailure)
                return Result.Failure<PendingInvestment>(transactionResult.Error);

            var signedTxResult = await SignTransaction(walletWords, transactionResult.Value);
            if (signedTxResult.IsFailure)
            {
                return Result.Failure<PendingInvestment>(signedTxResult.Error);
            }

            var requestResult = await SendSignatureRequest(
                walletWords,
                projectId,
                signedTxResult.Value);

            if (requestResult.IsFailure)
                return Result.Failure<PendingInvestment>(requestResult.Error);


            var pendingInvestment = new PendingInvestment(
                projectId,
                investorKey,
                amount.Sats,
                signedTxResult.Value.Transaction.GetHash().ToString(),
                signedTxResult.Value.Transaction.ToHex());

            return Result.Success(pendingInvestment);
        }
        catch (Exception ex)
        {
            return Result.Failure<PendingInvestment>($"Error creating investment transaction: {ex.Message}");
        }
    }

    private async Task<Result<TransactionInfo>> SignTransaction(WalletWords walletWords, Transaction transaction)
    {
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

        var changeAddressResult = Result.Try(() => accountInfo.GetNextChangeReceiveAddress())
            .Ensure(s => !string.IsNullOrEmpty(s), "Change address cannot be empty");

        if (changeAddressResult.IsFailure)
        {
            return Result.Failure<TransactionInfo>(changeAddressResult.Error);
        }

        var changeAddress = changeAddressResult.Value!;

        var feeEstimationResult = await Result.Try(walletOperations.GetFeeEstimationAsync);
        if (feeEstimationResult.IsFailure)
        {
            return Result.Failure<TransactionInfo>("Error getting fee estimation");
        }

        var feerate = feeEstimationResult.Value.FirstOrDefault()?.FeeRate ?? 1;

        var signedTransactionResult = Result.Try(() => walletOperations.AddInputsAndSignTransaction(
            changeAddress,
            transaction,
            walletWords,
            accountInfo,
            feerate));

        return signedTransactionResult;
    }

    private async Task<Result> SendSignatureRequest(WalletWords walletWords, ProjectId projectId, TransactionInfo signedTransaction)
    {
        try
        {
            var projectResult = await projectRepository.Get(projectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure("Project not found");
            }

            var project = projectResult.Value;
            string nostrPubKey = project.NostrPubKey;

            var senderPrivateKey = derivationOperations.DeriveNostrStorageKey(walletWords);
            var senderPrivateKeyHex = Encoders.Hex.EncodeData(senderPrivateKey.ToBytes());

            var signRequest = new SignRecoveryRequest
            {
                ProjectIdentifier = projectId.Value,
                TransactionHex = signedTransaction.Transaction.ToHex()
            };

            var serialized = serializer.Serialize(signRequest);

            var encryptedContent = await encryptionService.EncryptNostrContentAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                serialized);

            var tcs = new TaskCompletionSource<(bool Success, string Message)>();

            relayService.SendDirectMessagesForPubKeyAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                encryptedContent,
                response => { tcs.TrySetResult((response.Accepted, response.Message ?? "Sin mensaje")); });

            var timeoutTask = Task.Delay(8000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                return Result.Failure("Timeout al esperar respuesta del relay");
            }

            var result = await tcs.Task;
            return result.Success
                ? Result.Success()
                : Result.Failure($"Error del relay: {result.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error al enviar solicitud de firma: {ex.Message}");
        }
    }
}
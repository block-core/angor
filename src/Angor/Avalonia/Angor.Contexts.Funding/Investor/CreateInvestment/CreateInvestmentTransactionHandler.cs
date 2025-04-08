using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Requests.CreateInvestment;
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
using MediatR;
using SignRecoveryRequest = Angor.Contexts.Funding.Investor.Requests.CreateInvestment.SignRecoveryRequest;

namespace Angor.Contexts.Funding.Investor.CreateInvestment;

public class CreateInvestmentTransactionHandler(
    IProjectRepository projectRepository,
    IInvestorTransactionActions investorTransactionActions,
    ISeedwordsProvider seedwordsProvider,
    IWalletOperations walletOperations,
    IDerivationOperations derivationOperations,
    IEncryptionService encryptionService,
    ISerializer serializer,
    IRelayService relayService) : IRequestHandler<CreateInvestmentTransactionRequest, Result<InvestmentTransaction>>
{
    public async Task<Result<InvestmentTransaction>> Handle(CreateInvestmentTransactionRequest transactionRequest, CancellationToken cancellationToken)
    {
        try
        {
            // Get the project and investor key
            var projectResult = await projectRepository.Get(transactionRequest.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<InvestmentTransaction>(projectResult.Error);
            }

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(transactionRequest.WalletId);

            var walletWords = sensitiveDataResult.Value.ToWalletWords();

            var investorKey = derivationOperations.DeriveInvestorKey(walletWords, projectResult.Value.FounderKey);

            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<InvestmentTransaction>(sensitiveDataResult.Error);
            }

            var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(
                projectResult.Value.ToSharedModel(),
                investorKey,
                transactionRequest.Amount.Sats));

            if (transactionResult.IsFailure)
                return Result.Failure<InvestmentTransaction>(transactionResult.Error);

            var signedTxResult = await SignTransaction(walletWords, transactionResult.Value);
            if (signedTxResult.IsFailure)
            {
                return Result.Failure<InvestmentTransaction>(signedTxResult.Error);
            }

            var signedTxHex = signedTxResult.Value.Transaction.ToHex();
            var totalFee = signedTxResult.Value.TransactionFee;
            return new InvestmentTransaction(investorKey, 
                signedTxHex, 
                signedTxResult.Value.Transaction.GetHash().ToString(),
                new Amount(totalFee));
        }
        catch (Exception ex)
        {
            return Result.Failure<InvestmentTransaction>($"Error creating investment transaction: {ex.Message}");
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

    // TODO: Let's move it later to another API method
    private async Task<Result> SendSignatureRequest(WalletWords walletWords, Project project, TransactionInfo signedTransaction)
    {
        try
        {
            string nostrPubKey = project.NostrPubKey;

            var senderPrivateKey = derivationOperations.DeriveNostrStorageKey(walletWords);
            var senderPrivateKeyHex = Encoders.Hex.EncodeData(senderPrivateKey.ToBytes());

            var signRequest = new SignRecoveryRequest
            {
                ProjectIdentifier = project.Id.Value,
                TransactionHex = signedTransaction.Transaction.ToHex()
            };

            var serialized = serializer.Serialize(signRequest);

            var encryptedContent = await encryptionService.EncryptNostrContentAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                serialized);

            var tcs = new TaskCompletionSource<(bool Success, string Message)>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            // Registrar la cancelación para completar el TCS si hay timeout
            cts.Token.Register(() => 
                    tcs.TrySetResult((false, "Timeout al esperar respuesta del relay")), 
                useSynchronizationContext: false);

            relayService.SendDirectMessagesForPubKeyAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                encryptedContent,
                response => { tcs.TrySetResult((response.Accepted, response.Message ?? "Sin mensaje")); });

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
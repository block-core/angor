using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using SignRecoveryRequest = Angor.Contexts.Funding.Investor.Requests.CreateInvestment.SignRecoveryRequest;

namespace Angor.Contexts.Funding.Investor.CreateInvestment;

public class RequestFounderSignaturesHandler(
    IProjectRepository projectRepository,
    ISeedwordsProvider seedwordsProvider,
    IDerivationOperations derivationOperations,
    IEncryptionService encryptionService,
    INetworkConfiguration networkConfiguration,
    ISerializer serializer,
    IRelayService relayService) : IRequestHandler<RequestFounderSignaturesRequest, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RequestFounderSignaturesRequest requestFounderSignaturesRequest, CancellationToken cancellationToken)
    {
        var txnHex = requestFounderSignaturesRequest.InvestmentTransaction.SignedTxHex;
        var network = networkConfiguration.GetNetwork();
        var strippedInvestmentTransaction = network.CreateTransaction(txnHex);
        strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);
        
        var projectResult = await projectRepository.Get(requestFounderSignaturesRequest.ProjectId);
        
        if (projectResult.IsFailure)
        {
            return Result.Failure<Guid>(projectResult.Error);
        }
        
        var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(Guid.Empty);
        
        if (sensitiveDataResult.IsFailure)
        {
            return Result.Failure<Guid>(sensitiveDataResult.Error);
        }

        var walletWords = sensitiveDataResult.Value.ToWalletWords();
        var project = projectResult.Value;
        
        var sendSignatureResult = await SendSignatureRequest(walletWords, project, strippedInvestmentTransaction.ToHex());
        
        if (sendSignatureResult.IsFailure)
        {
            return Result.Failure<Guid>(sendSignatureResult.Error);
        }

        var requestId = sendSignatureResult.Value;
        // TODO: Don't forget to uncomment. We really need to save info
        //var saveResult = await Save(requestId, txnHex, requestFounderSignaturesRequest.InvestmentTransaction.InvestorKey, requestFounderSignaturesRequest.ProjectId);
        //return saveResult.Value;
        return Result.Success(Guid.Empty);
    }

    private async Task<Result<Guid>> Save(string requestId, string transactionHex, string investorKey, ProjectId projectId)
    {
        // TODO: Implement the save logic
        throw new NotImplementedException();
    }

    private async Task<Result<string>> SendSignatureRequest(WalletWords walletWords, Project project, string signedTransactionHex)
    {
        try
        {
            string nostrPubKey = project.NostrPubKey;

            var senderPrivateKey = derivationOperations.DeriveNostrStorageKey(walletWords);
            var senderPrivateKeyHex = Encoders.Hex.EncodeData(senderPrivateKey.ToBytes());

            var signRequest = new SignRecoveryRequest
            {
                ProjectIdentifier = project.Id.Value,
                TransactionHex = signedTransactionHex
            };

            var serialized = serializer.Serialize(signRequest);

            var encryptedContent = await encryptionService.EncryptNostrContentAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                serialized);
            
            var tcs = new TaskCompletionSource<(bool Success, string Message)>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            
            cts.Token.Register(() =>
                    tcs.TrySetResult((false, "Timeout while waiting the response from the relay")),
                useSynchronizationContext: false);

            var eventId = relayService.SendDirectMessagesForPubKeyAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                encryptedContent,
                response => { tcs.TrySetResult((response.Accepted, response.Message ?? "No message")); });

            var result = await tcs.Task;
            return result.Success
                ? Result.Success(eventId)
                : Result.Failure<string>($"Relay error: {result.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Error while sending the signature request {ex.Message}");
        }
    }
}
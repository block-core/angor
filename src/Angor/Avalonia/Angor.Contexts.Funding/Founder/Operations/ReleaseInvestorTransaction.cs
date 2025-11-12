using System.Text;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class ReleaseInvestorTransaction
{
    public record ReleaseInvestorTransactionRequest(string WalletId, ProjectId ProjectId, IEnumerable<string> InvestmentsEventIds) : IRequest<Result>;

    public class ReleaseInvestorTransactionHandler(ISignService signService, IProjectService projectService,
        INostrDecrypter nostrDecrypter, ISerializer serializer, IDerivationOperations derivationOperations,
        IInvestorTransactionActions investorTransactionActions, INetworkConfiguration networkConfiguration,
        IFounderTransactionActions founderTransactionActions, IEncryptionService encryptionService,
        ISeedwordsProvider seedwordsProvider) : IRequestHandler<ReleaseInvestorTransactionRequest, Result>
    {
        public async Task<Result> Handle(ReleaseInvestorTransactionRequest request, CancellationToken cancellationToken)
        {
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
                return Result.Failure(projectResult.Error);
            
            var requests = await FetchSignatureRequestsAsync(projectResult.Value.NostrPubKey, request.InvestmentsEventIds.ToList());

            var items = requests.ToList();
            
            var decryptResult = await DecryptMessages(request.WalletId, request.ProjectId, items);
            
            var wordsResult = await seedwordsProvider.GetSensitiveData(request.WalletId);
            if (wordsResult.IsFailure)
                return Result.Failure(wordsResult.Error);

            var key = derivationOperations.DeriveFounderRecoveryPrivateKey(wordsResult.Value.ToWalletWords(),
                projectResult.Value.FounderKey);
            
            var nostrPrivateKey =
                await derivationOperations.DeriveProjectNostrPrivateKeyAsync(wordsResult.Value.ToWalletWords(),
                    projectResult.Value.FounderKey);
            var nostrPrivateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            var failedSignatures = new StringBuilder();
            
            foreach (var payload in decryptResult.Value)
            {
                var result = await PerformReleaseSignature(payload, nostrPrivateKeyHex, Encoders.Hex.EncodeData(key.ToBytes()), projectResult.Value.ToProjectInfo());
                
                if (result.IsFailure)
                    failedSignatures.AppendLine(result.Error);;
            }
            
            return failedSignatures.Length > 0 ? Result.Failure(failedSignatures.ToString()) : Result.Success();
        }
        
        
        public Task<IEnumerable<SignatureReleaseItem>> FetchSignatureRequestsAsync(string projectNostrPubKey, List<string> eventIds)
        {
            var tcs = new TaskCompletionSource<IEnumerable<SignatureReleaseItem>>();

            var collectedItems = new List<SignatureReleaseItem>();

            try
            {
                signService.LookupInvestmentRequestsAsync(
                    projectNostrPubKey,
                    null,
                    null,
                    // On each message
                    (eventId, investorNostrPubKey, encryptedMessage, requestPublishedTIme) =>
                    {
                        if (!eventIds.Contains(eventId))
                            return; // Only keep the latest request

                        collectedItems.Add(new SignatureReleaseItem
                        {
                            investorNostrPubKey = investorNostrPubKey,
                            InvestmentRequestTime = requestPublishedTIme,
                            EncryptedSignRecoveryMessage = encryptedMessage,
                            EventId = eventId
                        });
                    },
                    // On end of messages
                    () => { tcs.SetResult(collectedItems); });
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        private async Task<Result<List<Payload>>> DecryptMessages(string walletId, ProjectId projectId, IEnumerable<SignatureReleaseItem> signaturesReleaseItems)
        {
            var list = new List<Payload>();
            
            foreach (var signatureReleaseItem in signaturesReleaseItems)
            {
                try
                {
                    var sigResJson = await nostrDecrypter.Decrypt(walletId, projectId,
                        new DirectMessage(signatureReleaseItem.EventId, signatureReleaseItem.investorNostrPubKey,
                            signatureReleaseItem.EncryptedSignRecoveryMessage,
                            signatureReleaseItem.InvestmentRequestTime));

                    if (sigResJson.IsFailure)
                        continue;

                    var item = serializer.Deserialize<SignRecoveryRequest>(sigResJson.Value);
                    
                    if (item is not null && 
                        item.ProjectIdentifier == projectId.Value &&
                        !string.IsNullOrWhiteSpace(item.InvestmentTransactionHex) &&
                        !string.IsNullOrWhiteSpace(item.UnfundedReleaseAddress))
                    {
                        list.Add(new Payload
                        {
                            SignatureReleaseItem = signatureReleaseItem,
                            SignRecoveryRequest = item
                        });
                    }
                }
                catch (Exception e)
                {
                    //TODO log filed requests
                }
            }
            
            return Result.Success(list);
        }

        private async Task<Result> PerformReleaseSignature(Payload payload, string nostrFounderPrivateKeyHex, string founderRecoveryPrivateKeyHex, ProjectInfo projectInfo)
        {
            try
            {
                var signatureInfo = CreateReleaseSignatures(payload.SignRecoveryRequest.InvestmentTransactionHex,
                    projectInfo, founderRecoveryPrivateKeyHex,
                    payload.SignRecoveryRequest.UnfundedReleaseAddress ?? payload.SignRecoveryRequest.UnfundedReleaseKey);

                var sigJson = serializer.Serialize(signatureInfo);


                var encryptedContent = await encryptionService.EncryptNostrContentAsync(nostrFounderPrivateKeyHex,
                    payload.SignatureReleaseItem.investorNostrPubKey, sigJson);

                signService.SendReleaseSigsToInvestor(encryptedContent, nostrFounderPrivateKeyHex,
                    payload.SignatureReleaseItem.investorNostrPubKey, payload.SignatureReleaseItem.EventId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
        }

        private SignatureInfo CreateReleaseSignatures(string transactionHex, ProjectInfo info, string founderSigningPrivateKey, string investorReleaseAddress)
    {
        var investorTrx = networkConfiguration.GetNetwork().CreateTransaction(transactionHex);

        // build sigs
        var recoveryTrx = investorTransactionActions.BuildUnfundedReleaseInvestorFundsTransaction(info, investorTrx, investorReleaseAddress);
        var sig = founderTransactionActions.SignInvestorRecoveryTransactions(info, transactionHex, recoveryTrx, founderSigningPrivateKey);

        if (!investorTransactionActions.CheckInvestorUnfundedReleaseSignatures(info, investorTrx, sig, investorReleaseAddress))
            throw new InvalidOperationException();

        sig.SignatureType = SignatureInfoType.Release;

        return sig;
    }
        
        private class Payload
        {
            public SignatureReleaseItem SignatureReleaseItem { get; set; }
            public SignRecoveryRequest SignRecoveryRequest { get; set; }
            
        }
    }
}
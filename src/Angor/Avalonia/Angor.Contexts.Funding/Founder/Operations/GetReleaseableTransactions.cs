using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetReleaseableTransactions
{
    public record GetReleaseableTransactionsRequest(Guid WalletId, ProjectId ProjectId) : IRequest<Result<IEnumerable<ReleaseableTransactionDto>>>;

    public class GetClaimableTransactionsHandler(ISignService signService, IProjectRepository projectRepository,
        INostrDecrypter nostrDecrypter, ISerializer serializer) : IRequestHandler<GetReleaseableTransactionsRequest, Result<IEnumerable<ReleaseableTransactionDto>>>
    {
        public async Task<Result<IEnumerable<ReleaseableTransactionDto>>> Handle(GetReleaseableTransactionsRequest request, CancellationToken cancellationToken)
        {
            
            var projectResult = await projectRepository.Get(request.ProjectId);
            if (projectResult.IsFailure)
                return Result.Failure<IEnumerable<ReleaseableTransactionDto>>(projectResult.Error);
            
            var requests = await FetchSignatureRequestsAsync(projectResult.Value.NostrPubKey);
            
            var items = requests.ToList();
            
            var decryptResult = DecryptMessages(request.WalletId, request.ProjectId, items);
            var approvalTask = FetchFounderApprovalsSignaturesAsync(projectResult.Value.NostrPubKey, items);
            var releaseTask = FetchFounderReleaseSignaturesAsync(projectResult.Value.NostrPubKey, items);
            
            await Task.WhenAll(approvalTask, releaseTask,decryptResult);

            var list = requests.Select(x => new ReleaseableTransactionDto
            {
                Approved = x.ApprovaleTime,
                Arrived = x.InvestmentRequestTime,
                Released = x.ReleaseSignaturesTime,
                InvestorAddress = x.SignRecoveryRequest
            });
                
            return Result.Success(list);
        }
        
        public class SignatureReleaseItem
        {
            public string investorNostrPubKey;
            public DateTime InvestmentRequestTime { get; set; }
            public string EncryptedSignRecoveryMessage { get; set; }
            public string EventId { get; set; }
            public DateTime ApprovaleTime { get; set; }
            public DateTime ReleaseSignaturesTime { get; set; }
            
            public string? SignRecoveryRequest { get; set; }
        }

        public Task<IEnumerable<SignatureReleaseItem>> FetchSignatureRequestsAsync(string projectNostrPubKey)
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
                        var existing = collectedItems.FirstOrDefault(x =>
                            x.investorNostrPubKey == investorNostrPubKey);

                        if (existing != null)
                        {
                            if (existing.InvestmentRequestTime >= requestPublishedTIme)
                                return; // Only keep the latest request

                            collectedItems.Remove(existing);
                        }

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

        private async Task<Result> DecryptMessages(Guid walletId, ProjectId projectId, IEnumerable<SignatureReleaseItem> signaturesReleaseItems)
        {
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
                    
                    var testingValidity = serializer.Deserialize<SignRecoveryRequest>(sigResJson.Value);
                    
                    if (testingValidity is not null && 
                        testingValidity.ProjectIdentifier == projectId.Value &&
                        !string.IsNullOrWhiteSpace(testingValidity.InvestmentTransactionHex) &&
                        !string.IsNullOrWhiteSpace(testingValidity.UnfundedReleaseAddress))
                    {
                        signatureReleaseItem.SignRecoveryRequest = signatureReleaseItem.SignRecoveryRequest;
                    }
                }
                catch (Exception e)
                {
                    signatureReleaseItem.SignRecoveryRequest = null; // should we remove the item instead?
                }
            }
            
            return Result.Success();
        }

        protected Task FetchFounderReleaseSignaturesAsync(string nostrPubKey, List<SignatureReleaseItem> signaturesReleaseItems)
        {
            var tcs = new TaskCompletionSource();

            signService.LookupSignedReleaseSigs(nostrPubKey,
                (item) =>
                {
                    var signatureRequest =
                        signaturesReleaseItems.FirstOrDefault(_ => _.investorNostrPubKey == item.ProfileIdentifier);

                    if (signatureRequest is null || // not found 
                        signatureRequest.ReleaseSignaturesTime > item.EventCreatedAt || // older message
                        (item.EventIdentifier != null &&
                         signatureRequest.EventId != item.EventIdentifier)) // releasing another request
                    {
                        return; // sig of an old request
                    }

                    signatureRequest.ReleaseSignaturesTime = item.EventCreatedAt;
                }, () =>
                {
                    tcs.SetResult();
                });
            
            return tcs.Task;
        }
        
        private Task FetchFounderApprovalsSignaturesAsync(string nostrPubKey, List<SignatureReleaseItem> signaturesReleaseItems)
        {
            var tcs = new TaskCompletionSource();

            signService.LookupInvestmentRequestApprovals(nostrPubKey,
                (investorNostrPubKey, timeEventCreated, reqEventId) =>
                {
                    var signatureRequest =
                        signaturesReleaseItems.FirstOrDefault(_ => _.investorNostrPubKey == investorNostrPubKey);

                    if (signatureRequest is null)
                        return; // ignore it could be a fake message

                    if (signatureRequest.ApprovaleTime > timeEventCreated)
                    {
                        return; // sig of an old request
                    }

                    if (reqEventId != null && signatureRequest.EventId != reqEventId)
                    {
                        return; // sig of an old request
                    }

                    signatureRequest.ApprovaleTime = timeEventCreated;
                },
                () =>
                {
                    tcs.SetResult();
                });
            
            return tcs.Task;
        }
    }
}
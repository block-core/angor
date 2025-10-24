using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Funding.Founder.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;
using Investment = Angor.Contexts.Funding.Founder.Domain.Investment;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetInvestments
{
    public class GetInvestmentsRequest(Guid walletId, ProjectId projectId) : IRequest<Result<IEnumerable<Investment>>>
    {
        public Guid WalletId { get; } = walletId;
        public ProjectId ProjectId { get; } = projectId;
    }

    public class GetInvestmentsHandler(
        IIndexerService indexerService,
        IProjectService projectRepository,
        ISignService signService,
        INostrDecrypter nostrDecrypter,
        INetworkConfiguration networkConfiguration,
        ISerializer serializer) : IRequestHandler<GetInvestmentsRequest, Result<IEnumerable<Investment>>>
    {
        public Task<Result<IEnumerable<Investment>>> Handle(GetInvestmentsRequest request, CancellationToken cancellationToken)
        {
            return GetInvestments(request);
        }

        private async Task<Result<IEnumerable<Investment>>> GetInvestments(GetInvestmentsRequest request)
        {
            var projectResult = await projectRepository.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(projectResult.Error);
            }

            var nostrPubKey = projectResult.Value.NostrPubKey;

            var dataResult = await GetInvestmentData(request, nostrPubKey);

            return dataResult
                .Map(data => data.requests.Select(req => CreateInvestment(req, data.approvals, data.alreadyInvested, projectResult.Value))
            );
        }
        
        private static InvestmentStatus DetermineInvestmentStatus(
            string eventId, 
            string transactionId, 
            IEnumerable<ApprovalMessage> approvals, 
            List<ProjectInvestment> alreadyInvested)
        {
            if (IsAlreadyInvested(transactionId, alreadyInvested))
                return InvestmentStatus.Invested;
    
            if (IsApproved(eventId, approvals))
                return InvestmentStatus.FounderSignaturesReceived;
    
            return InvestmentStatus.PendingFounderSignatures;
        }

        private static bool IsAlreadyInvested(string transactionId, List<ProjectInvestment> alreadyInvested)
        {
            return alreadyInvested.Any(investment => investment.TransactionId == transactionId);
        }

        private static bool IsApproved(string eventId, IEnumerable<ApprovalMessage> approvals)
        {
            return approvals.Any(approval => approval.EventIdentifier == eventId);
        }
        
        private Investment CreateInvestment(InvestmentRequest signRequest,
            IEnumerable<ApprovalMessage> approvals,
            List<ProjectInvestment> alreadyInvested,
            Project project)
        {
            var transaction = networkConfiguration.GetNetwork().CreateTransaction(signRequest.InvestmentTransactionHex);
            var amount = GetAmount(transaction, project);
            var transactionId = transaction.GetHash().ToString();
    
            var investmentStatus = DetermineInvestmentStatus(
                signRequest.EventId, 
                transactionId, 
                approvals, 
                alreadyInvested);
    
            return new Investment(
                signRequest.EventId, 
                signRequest.CreatedOn, 
                signRequest.InvestmentTransactionHex, 
                signRequest.InvestorNostrPubKey, 
                amount, 
                investmentStatus);
        }

        
        private Task<Result<(IEnumerable<InvestmentRequest> requests, IEnumerable<ApprovalMessage> approvals, List<ProjectInvestment> alreadyInvested)>> 
            GetInvestmentData(GetInvestmentsRequest request, string nostrPubKey)
        {
            return
                from requests in LookupRemoteRequests(request, nostrPubKey)
                from approvals in LookupRemoteApprovals(nostrPubKey)
                from alreadyInvested in LookupCurrentInvestments(request)
                select (requests, approvals, alreadyInvested);
        }

        private Task<Result<List<ProjectInvestment>>> LookupCurrentInvestments(GetInvestmentsRequest request)
        {
            return Result.Try(() => indexerService.GetInvestmentsAsync(request.ProjectId.Value));
        }

        private async Task<Result<IEnumerable<InvestmentRequest>>> LookupRemoteRequests(GetInvestmentsRequest request, string nostrPubKey)
        {
            return await InvestmentMessages(nostrPubKey)
                .Bind(messages => DecryptMessages(request, messages))
                .Bind(DeserializeRecoveryRequests)
                .MapEach(tuple => CreateInvestmentRequest(tuple.originalMessage, tuple.recoveryRequest));
        }
        
        private Task<Result<IEnumerable<(DirectMessage originalMessage, string decryptedContent)>>> DecryptMessages(
            GetInvestmentsRequest request, 
            IEnumerable<DirectMessage> messages)
        {
            return messages
                .Select(dm => nostrDecrypter.Decrypt(request.WalletId, request.ProjectId, dm)
                    .Map(decryptedContent => (originalMessage: dm, decryptedContent)))
                .CombineSequentially();
        }
        
        private Result<IEnumerable<(DirectMessage originalMessage, SignRecoveryRequest recoveryRequest)>> DeserializeRecoveryRequests(
            IEnumerable<(DirectMessage originalMessage, string decryptedContent)> decryptedMessages)
        {
            return decryptedMessages
                .Select(TryDeserializeRecoveryRequest)
                .Combine();
        }
        
        private Result<(DirectMessage originalMessage, SignRecoveryRequest recoveryRequest)> TryDeserializeRecoveryRequest(
            (DirectMessage originalMessage, string decryptedContent) decryption)
        {
            return Result.Try(() => serializer.Deserialize<SignRecoveryRequest>(decryption.decryptedContent))
                .EnsureNotNull(() => $"Cannot parse {decryption.decryptedContent} to SignRecoveryRequest")
                .Map(recoveryRequest => (decryption.originalMessage, recoveryRequest));
        }

        private static InvestmentRequest CreateInvestmentRequest(DirectMessage originalMessage, SignRecoveryRequest recoveryRequest)
        {
            return new InvestmentRequest(
                originalMessage.Created, 
                originalMessage.InvestorNostrPubKey, 
                recoveryRequest.InvestmentTransactionHex, 
                originalMessage.Id);
        }
        
        private async Task<Result<IEnumerable<ApprovalMessage>>> LookupRemoteApprovals(string nostrPubKey)
        {
            return await GetApprovedStatusObs()
                .ToList()
                .Select(list => list.AsEnumerable())
                .ToResult();

            IObservable<ApprovalMessage> GetApprovedStatusObs()
            {
                return Observable.Create<ApprovalMessage>(observer =>
                {
                    signService.LookupInvestmentRequestApprovals(nostrPubKey,
                        (profileIdentifier, created, content) => observer.OnNext(new ApprovalMessage(profileIdentifier, created, content)),
                        observer.OnCompleted
                    );

                    return Disposable.Empty;
                });
            }
        }

        private async Task<Result<IEnumerable<DirectMessage>>> InvestmentMessages(string nostrPubKey)
        {
            return await InvestmentMessagesObs()
                .ToList()
                .Select(list => list.AsEnumerable())
                .ToResult();

            IObservable<DirectMessage> InvestmentMessagesObs()
            {
                return Observable.Create<DirectMessage>(observer =>
                {
                    signService.LookupInvestmentRequestsAsync(nostrPubKey, null, null,
                        (id, pubKey, content, created) => observer.OnNext(new DirectMessage(id, pubKey, content, created)),
                        observer.OnCompleted
                    );

                    return Disposable.Empty;
                });
            }
        }

        private static long GetAmount(Transaction transaction, Project project)
        {
            return transaction.Outputs.AsIndexedOutputs()
                .Skip(2)
                .Take(project.Stages.Count())
                .Sum(x => x.TxOut.Value.Satoshi);
        }
    }

    private record ApprovalMessage(string ProfileIdentifier, DateTime Created, string EventIdentifier);
}
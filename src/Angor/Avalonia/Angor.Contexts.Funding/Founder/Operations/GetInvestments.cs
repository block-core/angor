using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

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
        IProjectRepository projectRepository,
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
            var projectResult = await projectRepository.Get(request.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(projectResult.Error);
            }

            var nostrPubKey = projectResult.Value.NostrPubKey;

            var dataResult = await GetInvestmentData(request, nostrPubKey);

            return dataResult
                .Map(data => data.requests.Select(request => CreateInvestment(request, data.approvals, data.alreadyInvested))
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
                return InvestmentStatus.Approved;
    
            return InvestmentStatus.Pending;
        }

        private static bool IsAlreadyInvested(string transactionId, List<ProjectInvestment> alreadyInvested)
        {
            return alreadyInvested.Any(investment => investment.TransactionId == transactionId);
        }

        private static bool IsApproved(string eventId, IEnumerable<ApprovalMessage> approvals)
        {
            return approvals.Any(approval => approval.EventIdentifier == eventId);
        }
        
        private Investment CreateInvestment(
            InvestmentRequest signRequest, 
            IEnumerable<ApprovalMessage> approvals, 
            List<ProjectInvestment> alreadyInvested)
        {
            var transaction = networkConfiguration.GetNetwork().CreateTransaction(signRequest.InvestmentTransactionHex);
            var amount = GetAmount(transaction);
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
                from requests in Requests(request, nostrPubKey)
                from approvals in Approvals(nostrPubKey)
                from alreadyInvested in AlreadyInvested(request)
                select (requests, approvals, alreadyInvested);
        }

        private Task<Result<List<ProjectInvestment>>> AlreadyInvested(GetInvestmentsRequest request)
        {
            return Result.Try(() => indexerService.GetInvestmentsAsync(request.ProjectId.Value));
        }

        private async Task<Result<IEnumerable<InvestmentRequest>>> Requests(GetInvestmentsRequest request, string nostrPubKey)
        {
            return await InvestmentMessages(nostrPubKey)
                .MapEach(message => nostrDecrypter.Decrypt(request.WalletId, request.ProjectId, message).Map(s => new { message, s })).CombineSequentially()
                .MapEach(decrypted => Result.Try(() => serializer.Deserialize<SignRecoveryRequest>(decrypted.s)).EnsureNotNull(() => $"Cannot parse {decrypted.s} to SignRecoveryRequest").Map(recoveryRequest => new { recoveryRequest, decrypted.message }))
                .Bind(results => results.Combine())
                .MapEach(arg => new InvestmentRequest(arg.message.Created, arg.message.InvestorNostrPubKey, arg.recoveryRequest.InvestmentTransactionHex, arg.message.Id));
        }

        private async Task<Result<IEnumerable<ApprovalMessage>>> Approvals(string nostrPubKey)
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

        private static long GetAmount(Transaction transaction)
        {
            return transaction.Outputs.AsIndexedOutputs()
                .Skip(2)
                .Take(transaction.Outputs.Count - 3)
                .Sum(x => x.TxOut.Value.Satoshi);
        }
    }

    private record ApprovalMessage(string ProfileIdentifier, DateTime Created, string EventIdentifier);
}
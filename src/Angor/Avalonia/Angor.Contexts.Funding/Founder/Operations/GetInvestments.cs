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
    public class GetInvestmentsRequest(Guid walletId, ProjectId projectId) : IRequest<Result<IEnumerable<Investment2>>>
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
        ISerializer serializer) : IRequestHandler<GetInvestmentsRequest, Result<IEnumerable<Investment2>>>
    {
        public Task<Result<IEnumerable<Investment2>>> Handle(GetInvestmentsRequest request, CancellationToken cancellationToken)
        {
            return GetInvestments(request);
        }

        private async Task<Result<IEnumerable<Investment2>>> GetInvestments(GetInvestmentsRequest request)
        {
            var projectResult = await projectRepository.Get(request.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment2>>(projectResult.Error);
            }

            var nostrPubKey = projectResult.Value.NostrPubKey;

            var tupleResult =
                from requests in Requests(request, nostrPubKey)
                from approvals in Approvals(nostrPubKey)
                from alreadyInvested in AlreadyInvested(request)
                select new { requests, approvals, alreadyInvested };

            return await tupleResult.Map(tuple =>
            {
                var list = new List<Investment2>();
                foreach (var signRequest in tuple.requests)
                {
                    var tx = networkConfiguration.GetNetwork().CreateTransaction(signRequest.InvestmentTransactionHex);
                    var amount = GetAmount(tx);
                    var isInvested = tuple.alreadyInvested.Any(x => x.TransactionId == tx.GetHash().ToString());
                    var isApproved = tuple.approvals.Any(message => message.EventIdentifier == signRequest.EventId);
                    var status = isInvested ? InvestmentStatus.Invested : isApproved ? InvestmentStatus.Approved : InvestmentStatus.Pending;
                    var investment = new Investment2(signRequest.EventId, DateTime.Now, signRequest.InvestmentTransactionHex, signRequest.InvestorNostrPubKey, amount, status);
                    list.Add(investment);
                }

                return list.AsEnumerable();
            });
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
            //.MapEach(recoveryRequest => new RequestMessage(recoveryRequest.));
        }

        private async Task<Result<IEnumerable<Investment>>> CombineInvestmentsAndApprovals(
            Guid walletId,
            Project project,
            IList<DirectMessage> investmentMessages,
            IList<ApprovalMessage> approvalMessages)
        {
            var combineInvestmentsAndApprovals = investmentMessages.Select(message =>
            {
                return nostrDecrypter.Decrypt(walletId, project.Id, message)
                    .MapTry(serializer.Deserialize<SignRecoveryRequest>)
                    .Map(request =>
                    {
                        var tx = networkConfiguration.GetNetwork().CreateTransaction(request!.InvestmentTransactionHex);
                        var approvalMessageFound = approvalMessages.FirstOrDefault(a => a.EventIdentifier == message.Id);
                        return new Investment(message.Created, tx.GetHash().ToString(), GetAmount(tx), request.InvestmentTransactionHex, message.InvestorNostrPubKey, approvalMessageFound?.EventIdentifier ?? string.Empty);
                    });
            }).CombineInOrder(", ");

            var result = await combineInvestmentsAndApprovals;
            return result;
        }

        private async Task<Result<IList<ApprovalMessage>>> Approvals(string nostrPubKey)
        {
            return await GetApprovedStatusObs()
                .ToList()
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

    public class Investment
    {
        public Investment(DateTime created, string investmentTransactionId, long amount, string investmentTransactionHex, string investorNostrPubKey, string nostrEventId)
        {
            Created = created;
            InvestmentTransactionId = investmentTransactionId;
            Amount = amount;
            InvestmentTransactionHex = investmentTransactionHex;
            InvestorNostrPubKey = investorNostrPubKey;
            NostrEventId = nostrEventId;
        }

        public DateTime Created { get; }

        public string InvestmentTransactionId { get; }
        public long Amount { get; }
        public string InvestmentTransactionHex { get; }
        public string InvestorNostrPubKey { get; }
        public string NostrEventId { get; }
        public string InvestorPublicKey { get; set; }
        public bool IsApproved => !string.IsNullOrEmpty(NostrEventId);
        public bool IsInvested => !string.IsNullOrEmpty(InvestorPublicKey);
    }

    private record ApprovalMessage(string ProfileIdentifier, DateTime Created, string EventIdentifier);

    private class Source
    {
        public Source(IList<DirectMessage> msgs, IList<ApprovalMessage> appr, List<ProjectInvestment> currentInvestments)
        {
            throw new NotImplementedException();
        }
    }
}

internal record InvestmentRequest(DateTime Created, string InvestorNostrPubKey, string InvestmentTransactionHex, string EventId);

public record Investment2(string EventId, DateTime CreatedOn, string InvestmentTransactionHex, string InvestorNostrPubKey, long Amount, InvestmentStatus Status);
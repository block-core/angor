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

            var investmentsMessages = await InvestmentMessages(nostrPubKey);
            var approvals = await ApprovalMessages(nostrPubKey);
            var investments = await investmentsMessages
                .Bind(i => approvals
                    .Map(a => CombineInvestmentsAndApprovals(request.WalletId, projectResult.Value, i, a)));

            var projectInvestments = await indexerService.GetInvestmentsAsync(request.ProjectId.Value);

            foreach (var investment in investments.Value)
            {
                investment.InvestorPublicKey = projectInvestments
                                                   .FirstOrDefault(p => p.TransactionId == investment.InvestmentTransactionId)?.InvestorPublicKey ?? string.Empty;
            }

            return investments;
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

        private async Task<Result<IList<ApprovalMessage>>> ApprovalMessages(string nostrPubKey)
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

        private async Task<Result<IList<DirectMessage>>> InvestmentMessages(string nostrPubKey)
        {
            return await InvestmentMessagesObs()
                .ToList()
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

        private long GetAmount(Transaction investorTrx)
        {
            return investorTrx.Outputs.AsIndexedOutputs()
                .Skip(2)
                .Take(investorTrx.Outputs.Count - 3)
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
}
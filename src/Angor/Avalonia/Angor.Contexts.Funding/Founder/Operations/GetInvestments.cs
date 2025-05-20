using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using NBitcoin.Protocol;
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

            var investments = await InvestmentMessages(nostrPubKey);
            var approvals = await ApprovalMessages(nostrPubKey);

            return await investments
                .Bind(i => approvals
                    .Map(a => CombineInvestmentsAndApprovals(request.WalletId, projectResult.Value, i, a))); 
        }

        private async Task<Result<IEnumerable<Investment>>> CombineInvestmentsAndApprovals(Guid walletId, Project project, IList<DirectMessage> messages, IList<ApprovalMessage> approvals)
        {
            var combineInvestmentsAndApprovals = messages.Select(message =>
            {
                return nostrDecrypter.Decrypt(walletId, project.Id, message)
                    .MapTry(serializer.Deserialize<SignRecoveryRequest>)
                    .Map(request =>
                    {
                        var isApproved = approvals.Any(a => a.EventIdentifier == message.Id);
                        return new Investment(message.Created, GetAmount(request.InvestmentTransactionHex), request.InvestmentTransactionHex, message.InvestorNostrPubKey, message.Id, isApproved);;
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

        private long GetAmount(string transactionHex)
        {
            var investorTrx = networkConfiguration.GetNetwork().CreateTransaction(transactionHex);

            return investorTrx.Outputs.AsIndexedOutputs()
                .Skip(2)
                .Take(investorTrx.Outputs.Count - 3)
                .Sum(x => x.TxOut.Value.Satoshi);
        }
    }

    public record Investment(DateTime Created, long Amount, string InvestmentTransactionHex, string InvestorNostrPubKey, string NostrEventId, bool IsApproved);

    private record ApprovalMessage(string ProfileIdentifier, DateTime Created, string EventIdentifier);
}
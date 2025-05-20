using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using NBitcoin.Protocol;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
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
        NostrQueryClient nostrQueryClient,
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

            var combineAndMap = await investments.Bind(i => approvals.Map(a => CombineInvestmentsAndApprovals(request.WalletId, projectResult.Value, i, a)));
            return combineAndMap; 
        }

        private async Task<Result<IEnumerable<Investment>>> CombineInvestmentsAndApprovals(Guid walletId, Project project, IList<NostrMessage> messages, IList<NostrMessage> approvals)
        {
            var combineInvestmentsAndApprovals = messages.Select(message =>
            {
                return nostrDecrypter.Decrypt(walletId, project.Id, message)
                    .MapTry(serializer.Deserialize<SignRecoveryRequest>)
                    .Map(async request =>
                    {
                        var isApproved = approvals.Any(a => a.Id == message.Id);
                        return new Investment(message.Created, GetAmount(request.InvestmentTransactionHex), request.InvestmentTransactionHex, message.InvestorNostrPubKey, message.Id, isApproved);;
                    });
            }).CombineInOrder(", ");

            var result = await combineInvestmentsAndApprovals;
            return result;
        }

        private bool IsApproved(NostrMessage investmentMessage, IList<NostrMessage> approvedMessages)
        {
            return false;
        }

        private async Task<Result<IList<NostrMessage>>> ApprovalMessages(string nostrPubKey)
        {
            IObservable<NostrMessage> GetApprovedStatusObs(string nostrPubKey)
            {
                return Observable.Create<NostrMessage>(observer =>
                {
                    signService.LookupInvestmentRequestApprovals(nostrPubKey,
                        (id, created, content) => observer.OnNext(new NostrMessage(id, nostrPubKey, content, created)),
                        observer.OnCompleted
                    );

                    return Disposable.Empty;
                });
            }
            
            return await GetApprovedStatusObs(nostrPubKey)
                .ToList()
                .ToResult();
        }

        private async Task<Result<IList<NostrMessage>>> InvestmentMessages(string nostrPubKey)
        {
            IObservable<NostrMessage> InvesmentMessagesObs(string nostrPubKey)
            {
                return Observable.Create<NostrMessage>(observer =>
                {
                    signService.LookupInvestmentRequestsAsync(nostrPubKey, null, null,
                        (id, pubKey, content, created) => observer.OnNext(new NostrMessage(id, pubKey, content, created)),
                        observer.OnCompleted
                    );

                    return Disposable.Empty;
                });
            }
            
            return await InvesmentMessagesObs(nostrPubKey)
                .ToList()
                .ToResult();
        }

        private async Task<Result<bool>> IsApproved(NostrMessage nostrMessage, Project project)
        {
            var filter = new NostrFilter
            {
                Kinds = new[] { NostrKind.EncryptedDm },
                Authors = new[] { project.NostrPubKey }
            };

            filter.AddTag("p", nostrMessage.InvestorNostrPubKey);
            filter.AddTag("e", nostrMessage.Id);

            var approved = await nostrQueryClient.Query(filter, TimeSpan.FromSeconds(5))
                .Any(r => r.Event?.Tags?.FindFirstTagValue("subject") == "Re:Investment offer")
                .ToResult("Failed to query events")
                .WithTimeout(TimeSpan.FromSeconds(7), "Timeout while waiting for the approval");

            return approved;
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
}
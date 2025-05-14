using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
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
            return GetInvestments(request).WithTimeout(TimeSpan.FromSeconds(5));
        }

        private async Task<Result<IEnumerable<Investment>>> GetInvestments(GetInvestmentsRequest request)
        {
            var projectResult = await projectRepository.Get(request.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(projectResult.Error);
            }

            // TODO: This needs to be done in a better way using the NostrQueryClient 
            var nostrPubKey = projectResult.Value.NostrPubKey;
            var investingMessages = await InvestmentMessages(nostrPubKey)
                .ToList()
                .ToResult();

            var decryptedInvestments = new List<Result<Investment>>();
            foreach (var investingMessage in investingMessages.Value)
            {
                var msg = await DecryptInvestmentMessage(request.WalletId, projectResult.Value, investingMessage);

                decryptedInvestments.Add(msg);
            }

            return decryptedInvestments.Combine();
        }

        private Task<Result<Investment>> DecryptInvestmentMessage(Guid walletId, Project project, NostrMessage nostrMessage)
        {
            return
                from decrypted in nostrDecrypter.Decrypt(walletId, project.Id, nostrMessage)
                from signRecoveryRequest in Result.Try(() => serializer.Deserialize<SignRecoveryRequest>(decrypted))
                from isApproved in IsApproved(nostrMessage, project)
                select new Investment(nostrMessage.Created,
                    GetAmount(signRecoveryRequest),
                    signRecoveryRequest.InvestmentTransactionHex,
                    nostrMessage.InvestorNostrPubKey,
                    nostrMessage.Id,
                    isApproved);
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

        private IObservable<NostrMessage> InvestmentMessages(string nostrPubKey)
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

        private long GetAmount(SignRecoveryRequest signRecoveryRequest)
        {
            var investorTrx = networkConfiguration.GetNetwork().CreateTransaction(signRecoveryRequest.InvestmentTransactionHex);

            return investorTrx.Outputs.AsIndexedOutputs()
                .Skip(2)
                .Take(investorTrx.Outputs.Count - 3)
                .Sum(x => x.TxOut.Value.Satoshi);
        }
    }

    public record Investment(DateTime Created, long Amount, string InvestmentTransactionHex, string InvestorNostrPubKey, string NostrEventId, bool IsApproved);
}
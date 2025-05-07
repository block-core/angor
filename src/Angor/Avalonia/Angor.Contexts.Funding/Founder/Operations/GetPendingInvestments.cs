using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public class GetPendingInvestments
{
    public class GetPendingInvestmentsRequest(Guid walletId, ProjectId projectId) : IRequest<Result<IEnumerable<PendingInvestmentDto>>>
    {
        public Guid WalletId { get; } = walletId;
        public ProjectId ProjectId { get; } = projectId;
    }
    
    public class GetPendingInvestmentsHandler(IProjectRepository projectRepository, 
        ISignService signService, 
        INostrDecrypter nostrDecrypter, 
        INetworkConfiguration networkConfiguration,
        ISerializer serializer) : IRequestHandler<GetPendingInvestmentsRequest, Result<IEnumerable<PendingInvestmentDto>>>
    {
        public async Task<Result<IEnumerable<PendingInvestmentDto>>> Handle(GetPendingInvestmentsRequest request, CancellationToken cancellationToken)
        {
            var project = await projectRepository.Get(request.ProjectId);
            if (project.IsFailure)
            {
                return Result.Failure<IEnumerable<PendingInvestmentDto>>(project.Error);
            }
            
            var nostrPubKey = project.Value.NostrPubKey;
            var investingMessages = InvestmentMessages(nostrPubKey);
            var pendingInvestmentResults = await investingMessages.SelectMany(nostrMessage => DecryptInvestmentMessage(request.WalletId, project, nostrMessage)).ToList();

            return pendingInvestmentResults.Combine();
        }

        private Task<Result<PendingInvestmentDto>> DecryptInvestmentMessage(Guid walletId, Result<Project> project, NostrMessage nostrMessage)
        {
            return from decrypted in nostrDecrypter.Decrypt(walletId, project.Value.Id, nostrMessage)
                from signRecoveryRequest in Result.Try(() => serializer.Deserialize<SignRecoveryRequest>(decrypted))
                select new PendingInvestmentDto(nostrMessage.Created, GetAmount(signRecoveryRequest), nostrMessage.InvestorNostrPubKey);
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

        private decimal GetAmount(SignRecoveryRequest signRecoveryRequest)
        {
            var investorTrx = networkConfiguration.GetNetwork().CreateTransaction(signRecoveryRequest.InvestmentTransactionHex);

            return investorTrx.Outputs.AsIndexedOutputs()
                .Skip(2)
                .Take(investorTrx.Outputs.Count - 3)
                .Sum(x => x.TxOut.Value.Satoshi);
        }
    }

    public record PendingInvestmentDto(DateTime Created, decimal Amount, string InvestorNostrPubKey)
    {
    }
}
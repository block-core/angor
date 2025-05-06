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
    public class GetPendingInvestmentsRequest(Guid walletId, ProjectId projectId) : IRequest<Result<IEnumerable<PendingDto>>>
    {
        public Guid WalletId { get; } = walletId;
        public ProjectId ProjectId { get; } = projectId;
    }
    
    public class GetPendingInvestmentsHandler(IProjectRepository projectRepository, 
        ISignService signService, 
        INostrDecrypter nostrDecrypter, 
        INetworkConfiguration networkConfiguration,
        ISerializer serializer) : IRequestHandler<GetPendingInvestmentsRequest, Result<IEnumerable<PendingDto>>>
    {
        public async Task<Result<IEnumerable<PendingDto>>> Handle(GetPendingInvestmentsRequest request, CancellationToken cancellationToken)
        {
            var project = await projectRepository.Get(request.ProjectId);
            if (project.IsFailure)
            {
                return Result.Failure<IEnumerable<PendingDto>>(project.Error);
            }
            
            var nostrPubKey = project.Value.NostrPubKey;

            var pendingObs = Observable.Create<NostrMessage>(observer =>
            {
                signService.LookupInvestmentRequestsAsync(nostrPubKey, null, null,
                    (id, pubKey, content, created) => observer.OnNext(new NostrMessage(id, pubKey, content, created)),
                    observer.OnCompleted
                );

                return Disposable.Empty;
            });

            var list = await pendingObs.SelectMany(nostrMessage =>
            {
                return from decrypted in nostrDecrypter.Decrypt(request.WalletId, project.Value.Id, nostrMessage)
                    from sign in Result.Try(() => serializer.Deserialize<SignRecoveryRequest>(decrypted))
                    select new PendingDto(nostrMessage.Created, GetAmount(sign), nostrMessage.InvestorNostrPubKey);
            }).ToList();

            return list.Combine();
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

    public record PendingDto(DateTime Created, decimal Amount, string InvestorNostrPubKey);
}
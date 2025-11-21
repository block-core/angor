using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Investment = Angor.Contexts.Funding.Founder.Domain.Investment;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetInvestments
{
    public class GetInvestmentsRequest(WalletId walletId, ProjectId projectId) : IRequest<Result<IEnumerable<Investment>>>
    {
        public WalletId WalletId { get; } = walletId;
        public ProjectId ProjectId { get; } = projectId;
    }

    public class GetInvestmentsHandler(
        IAngorIndexerService angorIndexerService,
        IProjectService projectService,
        INetworkConfiguration networkConfiguration,
        IInvestmentHandshakeService HandshakeService) : IRequestHandler<GetInvestmentsRequest, Result<IEnumerable<Investment>>>
    {
        public Task<Result<IEnumerable<Investment>>> Handle(GetInvestmentsRequest request, CancellationToken cancellationToken)
        {
            return GetInvestments(request);
        }

        private async Task<Result<IEnumerable<Investment>>> GetInvestments(GetInvestmentsRequest request)
        {
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(projectResult.Error);
            }

            var nostrPubKey = projectResult.Value.NostrPubKey;

            // Sync investment Handshakes from Nostr to database
            var syncResult = await HandshakeService.SyncHandshakesFromNostrAsync(
                request.WalletId, 
                request.ProjectId, 
                nostrPubKey);

            if (syncResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(syncResult.Error);
            }

            // Get Handshakes from database
            var HandshakesResult = await HandshakeService.GetHandshakesAsync(
                request.WalletId, 
                request.ProjectId);

            if (HandshakesResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(HandshakesResult.Error);
            }

            var alreadyInvestedResult = await LookupCurrentInvestments(request);
            if (alreadyInvestedResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(alreadyInvestedResult.Error);
            }

            var investments = HandshakesResult.Value
                .OrderByDescending(req => req.CreatedOn)
                .Select(conv => CreateInvestmentFromHandshake(conv, alreadyInvestedResult.Value, projectResult.Value))
                .ToList();

            return Result.Success<IEnumerable<Investment>>(investments);
        }
        
        private InvestmentStatus DetermineInvestmentStatus(
            InvestmentHandshake Handshake,
            List<ProjectInvestment> alreadyInvested)
        {
            if (string.IsNullOrEmpty(Handshake.InvestmentTransactionHex))
                return InvestmentStatus.PendingFounderSignatures;

            var transaction = networkConfiguration.GetNetwork().CreateTransaction(Handshake.InvestmentTransactionHex);
            var transactionId = transaction.GetHash().ToString();

            if (IsAlreadyInvested(transactionId, alreadyInvested))
                return InvestmentStatus.Invested;
    
            if (Handshake.Status == InvestmentRequestStatus.Approved)
                return InvestmentStatus.FounderSignaturesReceived;
    
            return InvestmentStatus.PendingFounderSignatures;
        }

        private static bool IsAlreadyInvested(string transactionId, List<ProjectInvestment> alreadyInvested)
        {
            return alreadyInvested.Any(investment => investment.TransactionId == transactionId);
        }
        
        private Investment CreateInvestmentFromHandshake(
            InvestmentHandshake Handshake,
            List<ProjectInvestment> alreadyInvested,
            Project project)
        {
            if (string.IsNullOrEmpty(Handshake.InvestmentTransactionHex))
            {
                // Invalid investment - missing transaction hex
                return new Investment(
                    Handshake.RequestEventId,
                    Handshake.RequestCreated,
                    string.Empty,
                    Handshake.InvestorNostrPubKey,
                    0,
                    InvestmentStatus.PendingFounderSignatures);
            }

            var transaction = networkConfiguration.GetNetwork().CreateTransaction(Handshake.InvestmentTransactionHex);
            var amount = GetAmount(transaction, project);
    
            var investmentStatus = DetermineInvestmentStatus(Handshake, alreadyInvested);
    
            return new Investment(
                Handshake.RequestEventId,
                Handshake.RequestCreated,
                Handshake.InvestmentTransactionHex,
                Handshake.InvestorNostrPubKey,
                amount,
                investmentStatus);
        }

        private Task<Result<List<ProjectInvestment>>> LookupCurrentInvestments(GetInvestmentsRequest request)
        {
            return Result.Try(() => angorIndexerService.GetInvestmentsAsync(request.ProjectId.Value));
        }

        private static long GetAmount(Transaction transaction, Project project)
        {
            return transaction.Outputs.AsIndexedOutputs()
                .Skip(2)
                .Take(project.Stages.Count())
                .Sum(x => x.TxOut.Value.Satoshi);
        }
    }
}
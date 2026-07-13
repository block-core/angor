using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Investment = Angor.Sdk.Funding.Founder.Domain.Investment;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class GetProjectInvestments
{
    public class GetProjectInvestmentsRequest(WalletId walletId, ProjectId projectId) : IRequest<Result<GetProjectInvestmentsResponse>>
    {
        public WalletId WalletId { get; } = walletId;
        public ProjectId ProjectId { get; } = projectId;
    }

    public record GetProjectInvestmentsResponse(IEnumerable<Investment> Investments);

    public class GetProjectInvestmentsHandler(
        IAngorIndexerService angorIndexerService,
        IIndexerService indexerService,
        IProjectService projectService,
        INetworkConfiguration networkConfiguration,
        IInvestmentHandshakeService HandshakeService) : IRequestHandler<GetProjectInvestmentsRequest, Result<GetProjectInvestmentsResponse>>
    {
        public Task<Result<GetProjectInvestmentsResponse>> Handle(GetProjectInvestmentsRequest request, CancellationToken cancellationToken)
        {
            return GetProjectInvestments(request);
        }

        private async Task<Result<GetProjectInvestmentsResponse>> GetProjectInvestments(GetProjectInvestmentsRequest request)
        {
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<GetProjectInvestmentsResponse>(projectResult.Error);
            }

            var nostrPubKey = projectResult.Value.NostrPubKey;

            // Sync investment Handshakes from Nostr to database
            var syncResult = await HandshakeService.SyncHandshakesFromNostrAsync(
                request.WalletId, 
                request.ProjectId, 
                nostrPubKey);

            if (syncResult.IsFailure)
            {
                return Result.Failure<GetProjectInvestmentsResponse>(syncResult.Error);
            }

            // Get Handshakes from database
            var HandshakesResult = await HandshakeService.GetHandshakesAsync(
                request.WalletId, 
                request.ProjectId);

            if (HandshakesResult.IsFailure)
            {
                return Result.Failure<GetProjectInvestmentsResponse>(HandshakesResult.Error);
            }

            var alreadyInvestedResult = await LookupCurrentInvestments(request);
            if (alreadyInvestedResult.IsFailure)
            {
                return Result.Failure<GetProjectInvestmentsResponse>(alreadyInvestedResult.Error);
            }

            var investments = new List<Investment>();
            foreach (var conv in HandshakesResult.Value.OrderByDescending(req => req.RequestCreated))
            {
                investments.Add(await CreateInvestmentFromHandshake(conv, alreadyInvestedResult.Value, projectResult.Value));
            }

            return Result.Success(new GetProjectInvestmentsResponse(investments));
        }
        
        private InvestmentStatus DetermineInvestmentStatus(
            InvestmentHandshake Handshake,
            List<ProjectInvestment> alreadyInvested)
        {
            // Direct investments (below threshold) are already invested - no approval needed
            if (Handshake.IsDirectInvestment)
                return InvestmentStatus.Invested;
            
            // Check if cancelled
            if (Handshake.Status == InvestmentRequestStatus.Cancelled)
                return InvestmentStatus.Cancelled;
            
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
        
        private async Task<Investment> CreateInvestmentFromHandshake(
            InvestmentHandshake Handshake,
            List<ProjectInvestment> alreadyInvested,
            Project project)
        {
            // Handle direct investments (below threshold) - they only have transaction ID, not full hex
            if (Handshake.IsDirectInvestment)
            {
                var transactionId = Handshake.InvestmentTransactionId ?? string.Empty;
                var indexedInvestment = alreadyInvested.FirstOrDefault(i => i.TransactionId == transactionId);
                var amount = indexedInvestment?.TotalAmount ?? 0;

                // Fallback: the indexer investment list may not (yet) include this transaction.
                // Fetch the raw transaction by ID and sum its taproot (investment) outputs.
                if (amount == 0 && !string.IsNullOrEmpty(transactionId))
                {
                    try
                    {
                        var transactionHex = await indexerService.GetTransactionHexByIdAsync(transactionId);
                        if (!string.IsNullOrEmpty(transactionHex))
                        {
                            var directTransaction = networkConfiguration.GetNetwork().CreateTransaction(transactionHex);
                            amount = directTransaction.GetTotalInvestmentAmount();
                        }
                    }
                    catch
                    {
                        // Transaction lookup is best-effort; leave amount at 0 if the indexer fails.
                    }
                }

                return new Investment(
                    Handshake.RequestEventId,
                    Handshake.RequestCreated,
                    transactionId, // Use transaction ID instead of hex
                    Handshake.InvestorNostrPubKey,
                    amount,
                    InvestmentStatus.Invested);
            }
            
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
            var amount2 = transaction.GetTotalInvestmentAmount();
    
            var investmentStatus = DetermineInvestmentStatus(Handshake, alreadyInvested);
    
            return new Investment(
                Handshake.RequestEventId,
                Handshake.RequestCreated,
                Handshake.InvestmentTransactionHex,
                Handshake.InvestorNostrPubKey,
                amount2,
                investmentStatus);
        }

        private Task<Result<List<ProjectInvestment>>> LookupCurrentInvestments(GetProjectInvestmentsRequest request)
        {
            return Result.Try(() => angorIndexerService.GetInvestmentsAsync(request.ProjectId.Value));
        }
    }
}
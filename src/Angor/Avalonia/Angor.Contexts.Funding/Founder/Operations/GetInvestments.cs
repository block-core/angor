using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Domain;
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
        IInvestmentConversationService conversationService) : IRequestHandler<GetInvestmentsRequest, Result<IEnumerable<Investment>>>
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

            // Sync investment conversations from Nostr to database
            var syncResult = await conversationService.SyncConversationsFromNostrAsync(
                request.WalletId, 
                request.ProjectId, 
                nostrPubKey);

            if (syncResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(syncResult.Error);
            }

            // Get conversations from database
            var conversationsResult = await conversationService.GetConversationsAsync(
                request.WalletId, 
                request.ProjectId);

            if (conversationsResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(conversationsResult.Error);
            }

            var alreadyInvestedResult = await LookupCurrentInvestments(request);
            if (alreadyInvestedResult.IsFailure)
            {
                return Result.Failure<IEnumerable<Investment>>(alreadyInvestedResult.Error);
            }

            var investments = conversationsResult.Value
                .OrderByDescending(req => req.CreatedOn)
                .Select(conv => CreateInvestmentFromConversation(conv, alreadyInvestedResult.Value, projectResult.Value))
                .ToList();

            return Result.Success<IEnumerable<Investment>>(investments);
        }
        
        private InvestmentStatus DetermineInvestmentStatus(
            InvestmentConversation conversation,
            List<ProjectInvestment> alreadyInvested)
        {
            if (string.IsNullOrEmpty(conversation.InvestmentTransactionHex))
                return InvestmentStatus.PendingFounderSignatures;

            var transaction = networkConfiguration.GetNetwork().CreateTransaction(conversation.InvestmentTransactionHex);
            var transactionId = transaction.GetHash().ToString();

            if (IsAlreadyInvested(transactionId, alreadyInvested))
                return InvestmentStatus.Invested;
    
            if (conversation.Status == InvestmentRequestStatus.Approved)
                return InvestmentStatus.FounderSignaturesReceived;
    
            return InvestmentStatus.PendingFounderSignatures;
        }

        private static bool IsAlreadyInvested(string transactionId, List<ProjectInvestment> alreadyInvested)
        {
            return alreadyInvested.Any(investment => investment.TransactionId == transactionId);
        }
        
        private Investment CreateInvestmentFromConversation(
            InvestmentConversation conversation,
            List<ProjectInvestment> alreadyInvested,
            Project project)
        {
            if (string.IsNullOrEmpty(conversation.InvestmentTransactionHex))
            {
                // Invalid investment - missing transaction hex
                return new Investment(
                    conversation.RequestEventId,
                    conversation.RequestCreated,
                    string.Empty,
                    conversation.InvestorNostrPubKey,
                    0,
                    InvestmentStatus.PendingFounderSignatures);
            }

            var transaction = networkConfiguration.GetNetwork().CreateTransaction(conversation.InvestmentTransactionHex);
            var amount = GetAmount(transaction, project);
    
            var investmentStatus = DetermineInvestmentStatus(conversation, alreadyInvested);
    
            return new Investment(
                conversation.RequestEventId,
                conversation.RequestCreated,
                conversation.InvestmentTransactionHex,
                conversation.InvestorNostrPubKey,
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
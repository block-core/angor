using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Shared;

public static class PublishTransaction
{
    public record PublishTransactionRequest(TransactionDraft TransactionDraft, Guid WalletId, ProjectId ProjectId) : IRequest<Result<string>>;
    
    public class Handler(IIndexerService indexerService, IPortfolioRepository investmentRepository) : IRequestHandler<PublishTransactionRequest, Result<string>>
    {
        public async Task<Result<string>> Handle(PublishTransactionRequest request, CancellationToken cancellationToken)
        {
            //TODO add validations and perhaps the wallet id to make sure we are publishing from the correct wallet only
            
            if (string.IsNullOrEmpty(request.TransactionDraft.SignedTxHex))
            {
                return Result.Failure<string>("Transaction signature cannot be empty");
            }

            if (request.TransactionDraft is InvestmentDraft investmentDraft)
            {
                await investmentRepository.Add(request.WalletId, new InvestmentRecord
                {
                    InvestmentTransactionHash = investmentDraft.TransactionId,
                    InvestmentTransactionHex = investmentDraft.SignedTxHex,
                    InvestorPubKey = investmentDraft.InvestorKey,
                    ProjectIdentifier = request.ProjectId.Value,
                });
            }

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure<string>("Operation was cancelled");

            var errorMessage = await indexerService.PublishTransactionAsync(request.TransactionDraft.SignedTxHex);
            
            return !string.IsNullOrEmpty(errorMessage) 
                ? Result.Failure<string>(errorMessage) 
                : Result.Success(request.TransactionDraft.TransactionId);
        }
    }
}
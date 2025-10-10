using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class ClaimEndOfProject
{
    public record ClaimEndOfProjectRequest(Guid WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<TransactionDraft>>;
    
    public class ClaimEndOfProjectHandler(IWalletOperations walletOperations, IDerivationOperations derivationOperations,
        IProjectRepository projectRepository, IInvestorTransactionActions investorTransactionActions,
        IPortfolioRepository investmentRepository, IIndexerService indexerService, ISeedwordsProvider provider) : IRequestHandler<ClaimEndOfProjectRequest, Result<TransactionDraft>>
    {
        public async Task<Result<TransactionDraft>> Handle(ClaimEndOfProjectRequest request, CancellationToken cancellationToken)
        {
            var words = await provider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
                return Result.Failure<TransactionDraft>(words.Error);
            
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(words.Value.ToWalletWords());
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            var investments = await investmentRepository.GetByWalletId(request.WalletId);
            if (investments.IsFailure)
                return Result.Failure<TransactionDraft>(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<TransactionDraft>("No investment found for this project");

            if (investment.InvestmentTransactionHex is null)
            {
                var lookupResult =  await Result.Try(() => indexerService.GetTransactionHexByIdAsync(investment.InvestmentTransactionHash));
                if (lookupResult.IsFailure)
                    return Result.Failure<TransactionDraft>("Could not find investment transaction in indexer: " + lookupResult.Error);
                investment.InvestmentTransactionHex = lookupResult.Value;
            }
            
            var project = await projectRepository.Get(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<TransactionDraft>(project.Error);
            
            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<TransactionDraft>("Could not get a change address");
            
            var transactionInfo = await indexerService.GetTransactionInfoByIdAsync(investment.InvestmentTransactionHash);

            if (transactionInfo is null)
                return Result.Failure<TransactionDraft>("Could not find transaction info");
            
            var stageIndex = transactionInfo.Outputs
                .Select((x, i) => i < 2 ? -1 // Skip the first two outputs (fee and op_return)
                    : string.IsNullOrEmpty(x.SpentInTransaction) ? i : -1)
                .First(x => x > 0);
            
            var endOfProjectTransaction = investorTransactionActions.RecoverEndOfProjectFunds(investment.InvestmentTransactionHex, project.Value.ToProjectInfo(), stageIndex,
                changeAddress, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()), new FeeEstimation(){FeeRate = request.SelectedFeeRate.SatsPerKilobyte});
            
           // var transactionId = await indexerService.PublishTransactionAsync(endOfProjectTransaction.Transaction.ToHex());

           return Result.Success(new TransactionDraft()
               {
                   SignedTxHex = endOfProjectTransaction.Transaction.ToHex(),
                   TransactionFee = new Amount(Money.Satoshis(endOfProjectTransaction.TransactionFee)),
                   TransactionId = endOfProjectTransaction.Transaction.GetHash().ToString()
               });
        }
    }
}


using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class ClaimEndOfProject
{
    public record ClaimEndOfProjectRequest(Guid WalletId, ProjectId ProjectId, int StageIndex) : IRequest<Result>;

    // TODO: Placeholder handler
    public class ClaimEndOfProjectHandler(IWalletOperations walletOperations, IDerivationOperations derivationOperations,
        IProjectRepository projectRepository, IInvestorTransactionActions investorTransactionActions,
        IInvestmentRepository investmentRepository, IIndexerService indexerService, ISeedwordsProvider provider) : IRequestHandler<ClaimEndOfProjectRequest, Result>
    {
        public async Task<Result> Handle(ClaimEndOfProjectRequest request, CancellationToken cancellationToken)
        {
            var fetchFees = await walletOperations.GetFeeEstimationAsync();
            var selectedFeeEstimation = fetchFees.OrderBy(x => x.FeeRate).First();
            
            var words = await provider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
                return Result.Failure(words.Error);
            
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(words.Value.ToWalletWords());
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            var investments = await investmentRepository.GetByWalletId(request.WalletId);
            if (investments.IsFailure)
                return Result.Failure(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure("No investment found for this project");

            if (investment.InvestmentTransactionHex is null)
            {
                var lookupResult =  await Result.Try(() => indexerService.GetTransactionHexByIdAsync(investment.InvestmentTransactionHash));
                if (lookupResult.IsFailure)
                    return Result.Failure("Could not find investment transaction in indexer: " + lookupResult.Error);
                investment.InvestmentTransactionHex = lookupResult.Value;
            }
            
            var project = await projectRepository.Get(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure(project.Error);
            
            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure("Could not get a change address");
            
            var endOfProjectTransaction = investorTransactionActions.RecoverEndOfProjectFunds(investment.InvestmentTransactionHex, project.Value.ToProjectInfo(), request.StageIndex,
                changeAddress, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()), selectedFeeEstimation);
            
            var transactionId = await indexerService.PublishTransactionAsync(endOfProjectTransaction.Transaction.ToHex());
            
            return string.IsNullOrEmpty(transactionId) ? Result.Success(transactionId) : Result.Failure("Failed to publish the transaction: " + transactionId);
        }
    }
}


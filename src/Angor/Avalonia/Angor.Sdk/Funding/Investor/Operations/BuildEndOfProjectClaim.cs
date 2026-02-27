using Angor.Sdk.Common;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Angor.Sdk.Funding.Projects;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class BuildEndOfProjectClaim
{
    public record BuildEndOfProjectClaimRequest(WalletId WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<BuildEndOfProjectClaimResponse>>;
    
    public record BuildEndOfProjectClaimResponse(EndOfProjectTransactionDraft TransactionDraft);
    
    public class BuildEndOfProjectClaimHandler(
        IDerivationOperations derivationOperations, IProjectService projectService, 
        IInvestorTransactionActions investorTransactionActions, IPortfolioService investmentService, 
        ISeedwordsProvider provider, ITransactionService transactionService,
        IWalletAccountBalanceService walletAccountBalanceService) : IRequestHandler<BuildEndOfProjectClaimRequest, Result<BuildEndOfProjectClaimResponse>>
    {
        public async Task<Result<BuildEndOfProjectClaimResponse>> Handle(BuildEndOfProjectClaimRequest request, CancellationToken cancellationToken)
        {
            var words = await provider.GetSensitiveData(request.WalletId.Value);
            if (words.IsFailure)
                return Result.Failure<BuildEndOfProjectClaimResponse>(words.Error);
            
            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(request.WalletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<BuildEndOfProjectClaimResponse>(accountBalanceResult.Error);
            
            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var investments = await investmentService.GetByWalletId(request.WalletId.Value);
            if (investments.IsFailure)
                return Result.Failure<BuildEndOfProjectClaimResponse>(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<BuildEndOfProjectClaimResponse>("No investment found for this project");

            if (investment.InvestmentTransactionHex is null)
            {
                investment.InvestmentTransactionHex =  await transactionService.GetTransactionHexByIdAsync(investment.InvestmentTransactionHash);
                if (investment.InvestmentTransactionHex is null)
                    return Result.Failure<BuildEndOfProjectClaimResponse>("Could not find investment transaction in indexer: " + investment.InvestmentTransactionHash);
            }
            
            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<BuildEndOfProjectClaimResponse>(project.Error);
            
            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<BuildEndOfProjectClaimResponse>("Could not get a change address");
            
            var transactionInfo = await transactionService.GetTransactionInfoByIdAsync(investment.InvestmentTransactionHash);

            if (transactionInfo is null)
                return Result.Failure<BuildEndOfProjectClaimResponse>("Could not find transaction info");
            
            var firstUnspentTaproot = transactionInfo.Outputs
                .Select((output, index) => (output, index))
                .FirstOrDefault(x => x.output.OutputType == "v1_p2tr" && string.IsNullOrEmpty(x.output.SpentInTransaction));

            if (firstUnspentTaproot.output is null)
                return Result.Failure<BuildEndOfProjectClaimResponse>("No unspent taproot output found in the investment transaction");

            // Stage number is 1-based: output index 2 = stage 1, output index 3 = stage 2, etc.
            var stageNumber = firstUnspentTaproot.index - 1;

            var endOfProjectTransaction = investorTransactionActions.RecoverEndOfProjectFunds(investment.InvestmentTransactionHex, project.Value.ToProjectInfo(), stageNumber,
                changeAddress, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()), new FeeEstimation(){FeeRate = request.SelectedFeeRate.SatsPerKilobyte});
            
            return Result.Success(new BuildEndOfProjectClaimResponse(new EndOfProjectTransactionDraft()
               {
                   SignedTxHex = endOfProjectTransaction.Transaction.ToHex(),
                   TransactionFee = new Amount(Money.Satoshis(endOfProjectTransaction.TransactionFee)),
                   TransactionId = endOfProjectTransaction.Transaction.GetHash().ToString()
               }));
        }
    }
}

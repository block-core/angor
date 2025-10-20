using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Serilog;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class CreateInvestment
{
    public record CreateInvestmentTransactionRequest(Guid WalletId, ProjectId ProjectId, Amount Amount, DomainFeerate FeeRate) 
        : IRequest<Result<InvestmentDraft>> { }
    
    public class CreateInvestmentTransactionHandler(
        INetworkConfiguration networkConfiguration,
        IProjectRepository projectRepository,
        IInvestorTransactionActions investorTransactionActions,
        ISeedwordsProvider seedwordsProvider,
        IWalletOperations walletOperations,
        IDerivationOperations derivationOperations,
        IPortfolioRepository investmentRepository,
        ILogger logger) : IRequestHandler<CreateInvestmentTransactionRequest, Result<InvestmentDraft>>
    {
        public async Task<Result<InvestmentDraft>> Handle(CreateInvestmentTransactionRequest transactionRequest, CancellationToken cancellationToken)
        {
            try
            {
                // Get the project and investor key
                var projectResult = await projectRepository.Get(transactionRequest.ProjectId);
                if (projectResult.IsFailure)
                {
                    return Result.Failure<InvestmentDraft>(projectResult.Error);
                }

                var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(transactionRequest.WalletId);

                var walletWords = sensitiveDataResult.Value.ToWalletWords();

                var investorKey = derivationOperations.DeriveInvestorKey(walletWords, projectResult.Value.FounderKey);

                if (sensitiveDataResult.IsFailure)
                {
                    return Result.Failure<InvestmentDraft>(sensitiveDataResult.Error);
                }

                var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(
                    projectResult.Value.ToProjectInfo(),
                    investorKey,
                    transactionRequest.Amount.Sats));

                if (transactionResult.IsFailure)
                    return Result.Failure<InvestmentDraft>(transactionResult.Error);

                var signedTxResult = await SignTransaction(walletWords, transactionResult.Value, transactionRequest.FeeRate.SatsPerKilobyte);
                if (signedTxResult.IsFailure)
                {
                    return Result.Failure<InvestmentDraft>(signedTxResult.Error);
                }

                var signedTxHex = signedTxResult.Value.Transaction.ToHex();
                var minorFee = signedTxResult.Value.TransactionFee;
                var angorFee = signedTxResult.Value.Transaction.Outputs.AsIndexedOutputs().FirstOrDefault()?.TxOut.Value.Satoshi ?? 0;
                    
                var trxId = signedTxResult.Value.Transaction.GetHash().ToString();

                bool penaltyDisabled = projectResult.Value.ToProjectInfo().IsPenaltyDisabled(transactionRequest.Amount.Sats);

                return new InvestmentDraft(investorKey)
                {
                    TransactionFee = new Amount(minorFee + angorFee),
                    MinerFee = new Amount(minorFee),
                    AngorFee = new Amount(angorFee),
                    SignedTxHex = signedTxHex,
                    TransactionId = trxId,
                    PenaltyDisabled = penaltyDisabled
                };
            }
            catch (Exception ex)
            {
                return Result.Failure<InvestmentDraft>($"Error creating investment transaction: {ex.Message}");
            }
        }

        private async Task<Result<TransactionInfo>> SignTransaction(WalletWords walletWords, Transaction transaction,
            long feerate)
        {
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            var changeAddressResult = Result.Try(() => accountInfo.GetNextChangeReceiveAddress())
                .Ensure(s => !string.IsNullOrEmpty(s), "Change address cannot be empty");

            if (changeAddressResult.IsFailure)
            {
                return Result.Failure<TransactionInfo>(changeAddressResult.Error);
            }

            var changeAddress = changeAddressResult.Value!;
            
            var signedTransactionResult = Result.Try(() => walletOperations.AddInputsAndSignTransaction(
                changeAddress,
                transaction,
                walletWords,
                accountInfo,
                feerate));

            return signedTransactionResult;
        }

        private async Task<Result<string>> PublishSignedTransactionAsync(TransactionInfo signedTransaction)
        {
            try
            {
                var response = await walletOperations.PublishTransactionAsync(networkConfiguration.GetNetwork(),
                    signedTransaction.Transaction);

                if (response.Success)
                    return Result.Success(signedTransaction.Transaction.GetHash().ToString());

                logger.Error(response.Message);

                return Result.Failure<string>("Failed to publish the transaction to the blockchain");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error publishing signed transaction");
                return Result.Failure<string>("An error occurred while publishing the transaction");
            }
        }
    }
}
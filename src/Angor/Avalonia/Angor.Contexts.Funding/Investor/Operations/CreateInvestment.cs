using Angor.Contests.CrossCutting;
using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class CreateInvestment
{
    public record CreateInvestmentTransactionRequest(Guid WalletId, ProjectId ProjectId, Amount Amount, DomainFeerate FeeRate) 
        : IRequest<Result<InvestmentDraft>> { }
    
    public class CreateInvestmentTransactionHandler(
        IProjectRepository projectRepository,
        IInvestorTransactionActions investorTransactionActions,
        ISeedwordsProvider seedwordsProvider,
        IWalletOperations walletOperations,
        IDerivationOperations derivationOperations,
        IGenericDocumentCollection<WalletAccountBalanceInfo> walletAccountBalanceCollection) : IRequestHandler<CreateInvestmentTransactionRequest, Result<InvestmentDraft>>
    {
        public async Task<Result<InvestmentDraft>> Handle(CreateInvestmentTransactionRequest transactionRequest, CancellationToken cancellationToken)
        {
            try
            {
                // Get the project and investor key
                var projectResult = await projectRepository.GetAsync(transactionRequest.ProjectId);
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

                var signedTxResult = await SignTransaction(transactionRequest.WalletId, walletWords, transactionResult.Value, transactionRequest.FeeRate.SatsPerKilobyte);
                if (signedTxResult.IsFailure)
                {
                    return Result.Failure<InvestmentDraft>(signedTxResult.Error);
                }

                var signedTxHex = signedTxResult.Value.Transaction.ToHex();
                var minorFee = signedTxResult.Value.TransactionFee;
                var angorFee = signedTxResult.Value.Transaction.Outputs.AsIndexedOutputs().FirstOrDefault()?.TxOut.Value.Satoshi ?? 0;
                    
                var trxId = signedTxResult.Value.Transaction.GetHash().ToString();
                
                return new InvestmentDraft(investorKey)
                {
                    TransactionFee = new Amount(minorFee + angorFee),
                    MinerFee = new Amount(minorFee),
                    AngorFee = new Amount(angorFee),
                    SignedTxHex = signedTxHex,
                    TransactionId = trxId,
                };
            }
            catch (Exception ex)
            {
                return Result.Failure<InvestmentDraft>($"Error creating investment transaction: {ex.Message}");
            }
        }

        private async Task<Result<TransactionInfo>> SignTransaction(Guid walletId, WalletWords walletWords, Transaction transaction,
            long feerate)
        {
            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceCollection.FindByIdAsync(walletId.ToString());
            if (accountBalanceResult.IsFailure || accountBalanceResult.Value is null)
                return Result.Failure<TransactionInfo>("Account balance information not found in database. Please refresh your wallet first.");
            
            var accountInfo = accountBalanceResult.Value.AccountBalanceInfo.AccountInfo;

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
    }
}
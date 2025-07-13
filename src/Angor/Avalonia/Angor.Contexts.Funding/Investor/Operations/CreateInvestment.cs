using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class CreateInvestment
{
    public class CreateInvestmentTransactionRequest : IRequest<Result<Draft>>
    {
        public Guid WalletId { get; }
        public ProjectId ProjectId { get; }
        public Amount Amount { get; }
        public DomainFeerate Feerate { get; }

        public CreateInvestmentTransactionRequest(Guid walletId, ProjectId projectId, Amount amount, DomainFeerate feerate)
        {
            WalletId = walletId;
            ProjectId = projectId;
            Amount = amount;
            Feerate = feerate;
        }
    }
    
    public record Draft(string InvestorKey, string SignedTxHex, string TransactionId, Amount TransactionFee)
    {
        public Amount MinerFee { get; set; } = new Amount(-1);
        public Amount AngorFee { get; set; } = new Amount(-1);
    }

    public class CreateInvestmentTransactionHandler(
        IProjectRepository projectRepository,
        IInvestorTransactionActions investorTransactionActions,
        ISeedwordsProvider seedwordsProvider,
        IWalletOperations walletOperations,
        IDerivationOperations derivationOperations) : IRequestHandler<CreateInvestmentTransactionRequest, Result<Draft>>
    {
        public async Task<Result<Draft>> Handle(CreateInvestmentTransactionRequest transactionRequest, CancellationToken cancellationToken)
        {
            try
            {
                // TODO: Don't forget to use the feerate to create the Draft. It's not used currently.
                // feerate is the feerate that the user selected as "transaction speed" in the UI.
                var feerate = transactionRequest.Feerate;
                
                // Get the project and investor key
                var projectResult = await projectRepository.Get(transactionRequest.ProjectId);
                if (projectResult.IsFailure)
                {
                    return Result.Failure<Draft>(projectResult.Error);
                }

                var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(transactionRequest.WalletId);

                var walletWords = sensitiveDataResult.Value.ToWalletWords();

                var investorKey = derivationOperations.DeriveInvestorKey(walletWords, projectResult.Value.FounderKey);

                if (sensitiveDataResult.IsFailure)
                {
                    return Result.Failure<Draft>(sensitiveDataResult.Error);
                }

                var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(
                    projectResult.Value.ToProjectInfo(),
                    investorKey,
                    transactionRequest.Amount.Sats));

                if (transactionResult.IsFailure)
                    return Result.Failure<Draft>(transactionResult.Error);

                var signedTxResult = await SignTransaction(walletWords, transactionResult.Value);
                if (signedTxResult.IsFailure)
                {
                    return Result.Failure<Draft>(signedTxResult.Error);
                }

                var signedTxHex = signedTxResult.Value.Transaction.ToHex();
                var totalFee = signedTxResult.Value.TransactionFee;
                return new Draft(investorKey,
                    signedTxHex,
                    signedTxResult.Value.Transaction.GetHash().ToString(),
                    new Amount(totalFee))
                {
                    // TODO: Please, fill this with the correct values
                    // MinerFee = new Amount(1234),
                    // AngorFee = new Amount(5678),
                };
            }
            catch (Exception ex)
            {
                return Result.Failure<Draft>($"Error creating investment transaction: {ex.Message}");
            }
        }

        private async Task<Result<TransactionInfo>> SignTransaction(WalletWords walletWords, Transaction transaction)
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

            var feeEstimationResult = await Result.Try(walletOperations.GetFeeEstimationAsync);
            if (feeEstimationResult.IsFailure)
            {
                return Result.Failure<TransactionInfo>("Error getting fee estimation");
            }

            var feerate = feeEstimationResult.Value.FirstOrDefault()?.FeeRate ?? 1;

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
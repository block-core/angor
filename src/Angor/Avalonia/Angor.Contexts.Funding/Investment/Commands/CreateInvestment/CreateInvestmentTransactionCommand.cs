using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investment.Commands.CreateInvestment;

public class CreateInvestmentTransactionCommand(
    IProjectRepository projectRepository,
    IInvestorTransactionActions investorTransactionActions,
    ISeedwordsProvider seedwordsProvider,
    IWalletOperations walletOperations,
    ISignatureRequestService signatureRequestService,
    IDerivationOperations derivationOperations,
    Guid walletId,
    ProjectId projectId,
    Amount amount)
{
    public async Task<Result<PendingInvestment>> Execute()
    {
        try
        {
            // 1. Obtener el proyecto y la clave del inversor
            var projectResult = await projectRepository.Get(projectId);
            if (projectResult.IsFailure)
                return Result.Failure<PendingInvestment>(projectResult.Error);

            var sensitiveData = await seedwordsProvider.GetSensitiveData(walletId);

            var walletWords = sensitiveData.Value.ToWalletWords();

            var investorKey = derivationOperations.DeriveInvestorKey(walletWords, projectResult.Value.FounderKey);
            
            if (sensitiveData.IsFailure)
                return Result.Failure<PendingInvestment>(sensitiveData.Error);

            // 2. Crear la transacción de inversión
            var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(
                projectResult.Value.ToSharedModel(), 
                investorKey, 
                amount.Sats));

            if (transactionResult.IsFailure)
                return Result.Failure<PendingInvestment>(transactionResult.Error);

            // 3. Firmar la transacción
            var signedTxResult = await SignTransaction(transactionResult.Value);
            if (signedTxResult.IsFailure)
                return Result.Failure<PendingInvestment>(signedTxResult.Error);

            // 4. Enviar solicitud de firmas al fundador
            var requestResult = await signatureRequestService.SendSignatureRequest(
                walletId,
                projectResult.Value.FounderKey,
                projectId,
                signedTxResult.Value);

            if (requestResult.IsFailure)
                return Result.Failure<PendingInvestment>(requestResult.Error);

            // 5. Crear y devolver la inversión pendiente
            return Result.Success(new PendingInvestment(
                projectId,
                investorKey,
                amount.Sats,
                signedTxResult.Value.Transaction.GetHash().ToString(),
                signedTxResult.Value.Transaction.ToHex()));
        }
        catch (Exception ex)
        {
            return Result.Failure<PendingInvestment>($"Error creating investment transaction: {ex.Message}");
        }
    }

    private async Task<Result<TransactionInfo>> SignTransaction(Transaction transaction)
    {
        var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensitiveDataResult.IsFailure)
        {
            return Result.Failure<TransactionInfo>(sensitiveDataResult.Error);
        }
        
        var sensitiveData = sensitiveDataResult.Value;
        var walletWords = new WalletWords()
        {
            Words = sensitiveData.Words,
            Passphrase = sensitiveData.Passphrase.GetValueOrDefault(""),
        };
        
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(sensitiveData.ToWalletWords());
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

    public class Factory(IProjectRepository projectRepository,
        IInvestorTransactionActions investorTransactionActions,
        ISeedwordsProvider seedwordsProvider,
        IWalletOperations walletOperations,
        ISignatureRequestService signatureRequestService, 
        IDerivationOperations derivationOperations)
    {
        public CreateInvestmentTransactionCommand Create(Guid walletId, ProjectId projectId, Amount amount)
        {
            return new CreateInvestmentTransactionCommand(
                projectRepository: projectRepository, 
                investorTransactionActions: investorTransactionActions, 
                seedwordsProvider: seedwordsProvider,
                walletOperations: walletOperations, 
                signatureRequestService: signatureRequestService, 
                derivationOperations: derivationOperations,
                walletId: walletId, 
                projectId: projectId, 
                amount: amount);
        }
    }
}
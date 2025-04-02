using Angor.Contests.CrossCutting;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Impl.Commands.Investment.CreateInvestment;

public class CreateInvestmentTransactionCommand(
    IProjectRepository projectRepository,
    IInvestorTransactionActions investorTransactionActions,
    IInvestorKeyProvider investorKeyProvider,
    IWalletOperations walletOperations,
    ISignatureRequestService signatureRequestService,
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

            var investorKeyResult = await investorKeyProvider.InvestorKey(walletId, projectResult.Value.FounderKey);
            if (investorKeyResult.IsFailure)
                return Result.Failure<PendingInvestment>(investorKeyResult.Error);

            // 2. Crear la transacción de inversión
            var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(
                projectResult.Value.ToSharedModel(), 
                investorKeyResult.Value, 
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
                investorKeyResult.Value,
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
        var sensitiveDataResult = await investorKeyProvider.GetSensitiveData(walletId);
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
        IInvestorKeyProvider investorKeyProvider,
        IWalletOperations walletOperations,
        ISignatureRequestService signatureRequestService)
    {
        public CreateInvestmentTransactionCommand Create(Guid walletId, ProjectId projectId, Amount amount)
        {
            return new CreateInvestmentTransactionCommand(
                projectRepository: projectRepository, 
                investorTransactionActions: investorTransactionActions, 
                investorKeyProvider: investorKeyProvider,
                walletOperations: walletOperations, 
                signatureRequestService: signatureRequestService, 
                walletId: walletId, 
                projectId: projectId, 
                amount: amount);
        }
    }
}
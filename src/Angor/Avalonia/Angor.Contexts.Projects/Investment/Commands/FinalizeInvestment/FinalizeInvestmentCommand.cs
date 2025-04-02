using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Projects.Projects.Domain;
using Angor.Shared;
using CSharpFunctionalExtensions;

public class FinalizeInvestmentCommand(
    IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IWalletOperations walletOperations,
    Guid walletId,
    INetworkConfiguration networkConfiguration,
    ProjectId projectId)
{
    public async Task<Result> Execute()
    {
        try
        {
            // 1. Obtener la inversión firmada
            var signedInvestmentResult = await investmentRepository.GetSignedInvestment(walletId, projectId);
            if (signedInvestmentResult.IsFailure)
                return Result.Failure(signedInvestmentResult.Error);

            var signedInvestment = signedInvestmentResult.Value;

            // 2. Publicar la transacción en la blockchain
            var network = networkConfiguration.GetNetwork();
            var transaction = network.CreateTransaction(signedInvestment.SignedTransactionHex);
            var broadcastResult = await walletOperations.PublishTransactionAsync(network, transaction);
            
            if (!broadcastResult.Success)
                return Result.Failure(broadcastResult.Message);

            // 3. Crear y guardar la inversión completada
            var investment = Investment.Create(
                signedInvestment.ProjectId,
                signedInvestment.InvestorPubKey,
                signedInvestment.Amount,
                signedInvestment.TransactionId);

            // 4. Guardar en el repositorio
            var saveResult = await investmentRepository.Add(walletId, investment);
            
            return saveResult.IsSuccess 
                ? Result.Success() 
                : Result.Failure(saveResult.Error);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error finalizando inversión: {ex.Message}");
        }
    }
}
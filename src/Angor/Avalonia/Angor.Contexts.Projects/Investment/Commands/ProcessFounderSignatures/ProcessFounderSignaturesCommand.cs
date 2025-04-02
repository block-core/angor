using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Projects.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Impl.Commands.Investment.ProcessFounderSignatures;

public class ProcessFounderSignaturesCommand(
    IProjectRepository projectRepository,
    IInvestorTransactionActions investorTransactionActions,
    Guid walletId,
    ProjectId projectId,
    INetworkConfiguration networkConfiguration,
    IInvestmentRepository investmentRepository,
    List<string> founderSignatures)
{
    public async Task<Result<SignedInvestment>> Execute()
    {
        try
        {
            // 1. Obtener la inversión pendiente
            var pendingInvestmentResult = await investmentRepository.GetPendingInvestment(walletId, projectId);
            if (pendingInvestmentResult.IsFailure)
                return Result.Failure<SignedInvestment>(pendingInvestmentResult.Error);

            var pendingInvestment = pendingInvestmentResult.Value;

            // 2. Obtener el proyecto
            var projectResult = await projectRepository.Get(projectId);
            if (projectResult.IsFailure)
                return Result.Failure<SignedInvestment>(projectResult.Error);

            var network = networkConfiguration.GetNetwork();

            // 3. Validar las firmas
            var transaction = network.CreateTransaction(pendingInvestment.SignedTransactionHex);
            var validSignatures = investorTransactionActions.CheckInvestorRecoverySignatures(
                projectResult.Value.ToSharedModel(),
                transaction,
                new SignatureInfo
                {
                    ProjectIdentifier = projectId.Value,
                    Signatures = founderSignatures.Select(signature => new SignatureInfoItem { Signature = signature }).ToList()
                });

            if (!validSignatures)
                return Result.Failure<SignedInvestment>("Las firmas del fundador no son válidas");

            // 4. Crear y devolver la inversión firmada
            return Result.Success(new SignedInvestment(
                pendingInvestment.ProjectId,
                pendingInvestment.InvestorPubKey,
                pendingInvestment.Amount,
                pendingInvestment.TransactionId,
                pendingInvestment.SignedTransactionHex,
                founderSignatures));
        }
        catch (Exception ex)
        {
            return Result.Failure<SignedInvestment>($"Error procesando firmas del fundador: {ex.Message}");
        }
    }
}
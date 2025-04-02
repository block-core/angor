using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investment.Commands.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using SignRecoveryRequest = Angor.Contexts.Funding.Investment.Commands.CreateInvestment.SignRecoveryRequest;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class NostrSignatureRequestService(
    IRelayService relayService,
    ISerializer serializer,
    IDerivationOperations derivationOperations,
    ISeedwordsProvider seedwordsProvider, 
    IProjectRepository projectRepository,
    IEncryptionService encryptionService)
    : ISignatureRequestService
{
public async Task<Result> SendSignatureRequest(Guid walletId, string founderPubKey, ProjectId projectId, TransactionInfo signedTransaction)
{
    try
    {
        // Obtener palabras semilla
        var wordsResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (wordsResult.IsFailure)
        {
            return Result.Failure($"Error al recuperar datos sensibles: {wordsResult.Error}");
        }

        // Buscar el proyecto para obtener NostrPubKey
        var projectResult = await projectRepository.Get(projectId);
        if (projectResult.IsFailure)
        {
            return Result.Failure("Project not found");
        }
        
        var project = projectResult.Value;
        string nostrPubKey = project.NostrPubKey;

        var words = wordsResult.Value.ToWalletWords();
        var senderPrivateKey = derivationOperations.DeriveNostrStorageKey(words);
        var senderPrivateKeyHex = Encoders.Hex.EncodeData(senderPrivateKey.ToBytes());
        
        // Derivar contraseña para encriptar, similar a InvestmentRepository
        var password = derivationOperations.DeriveNostrStoragePassword(words);

        var signRequest = new SignRecoveryRequest
        {
            ProjectIdentifier = projectId.Value,
            TransactionHex = signedTransaction.Transaction.ToHex()
        };

        // Serializar y ENCRIPTAR los datos antes de enviarlos
        var serialized = serializer.Serialize(signRequest);
        var encrypted = await encryptionService.EncryptData(serialized, password);
        
        var tcs = new TaskCompletionSource<(bool Success, string Message)>();

        // Enviar datos encriptados
        relayService.SendDirectMessagesForPubKeyAsync(
            senderPrivateKeyHex,
            nostrPubKey,
            encrypted,
            response => { tcs.TrySetResult((response.Accepted, response.Message ?? "Sin mensaje")); });

        var timeoutTask = Task.Delay(8000);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            return Result.Failure("Timeout al esperar respuesta del relay");
        }

        var result = await tcs.Task;
        return result.Success
            ? Result.Success()
            : Result.Failure($"Error del relay: {result.Message}");
    }
    catch (Exception ex)
    {
        return Result.Failure($"Error al enviar solicitud de firma: {ex.Message}");
    }
}}
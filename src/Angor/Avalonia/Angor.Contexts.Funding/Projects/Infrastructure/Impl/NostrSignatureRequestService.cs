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
    ISeedwordsProvider seedwordsProvider)
    : ISignatureRequestService
{
public async Task<Result> SendSignatureRequest(Guid walletId, string founderPubKey, ProjectId projectId, TransactionInfo signedTransaction)
{
    try {
        // Obtener palabras semilla
        var wordsResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (wordsResult.IsFailure)
        {
            return Result.Failure($"Error al recuperar datos sensibles: {wordsResult.Error}");
        }

        var words = wordsResult.Value.ToWalletWords();
        
        // Derivar claves para Nostr
        var senderPrivateKey = derivationOperations.DeriveNostrStorageKey(words);
        var senderPrivateKeyHex = Encoders.Hex.EncodeData(senderPrivateKey.ToBytes());

        // Crear y serializar la solicitud
        var signRequest = new SignRecoveryRequest
        {
            ProjectIdentifier = projectId.Value,
            TransactionHex = signedTransaction.Transaction.ToHex()
        };

        var serialized = serializer.Serialize(signRequest);
        
        // Usar TaskCompletionSource para manejar respuesta asíncrona
        var tcs = new TaskCompletionSource<(bool Success, string Message)>();

        // Enviar mensaje directo sin validaciones adicionales
        relayService.SendDirectMessagesForPubKeyAsync(
            senderPrivateKeyHex,
            founderPubKey,
            serialized,
            response => {
                tcs.TrySetResult((response.Accepted, response.Message ?? "Sin mensaje"));
            });

        // Esperar respuesta con timeout
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
    catch (Exception ex) {
        return Result.Failure($"Error al enviar solicitud de firma: {ex.Message}");
    }
}}
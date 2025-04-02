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
        try
        {
            // Obtener seedwords
            var wordsResult = await seedwordsProvider.GetSensitiveData(walletId);
            if (wordsResult.IsFailure)
            {
                return Result.Failure($"Error while retrieving sensitive data: {wordsResult.Error}");
            }

            var words = wordsResult.Value.ToWalletWords();

            // Derivar claves Nostr asegurándose que tengan el formato correcto
            var senderPrivateKey = derivationOperations.DeriveNostrStorageKey(words);
            var senderPrivateKeyHex = Encoders.Hex.EncodeData(senderPrivateKey.ToBytes());

            // Verificar que la clave pública del fundador tenga el formato correcto
            // (normalmente debería ser una clave hex de 64 caracteres)
            if (string.IsNullOrEmpty(founderPubKey) || founderPubKey.Length != 64)
            {
                return Result.Failure("La clave pública del fundador no tiene el formato correcto");
            }

            // Estructura de datos simple para evitar problemas de serialización
            var signRequest = new SignRecoveryRequest
            {
                ProjectIdentifier = projectId.Value,
                TransactionHex = signedTransaction.Transaction.ToHex()
            };

            var serialized = serializer.Serialize(signRequest);

            // Manejo de respuesta async con timeout
            var tcs = new TaskCompletionSource<(bool Success, string Message)>();

            relayService.SendDirectMessagesForPubKeyAsync(
                senderPrivateKeyHex,
                founderPubKey,
                serialized,
                response => { tcs.TrySetResult((response.Accepted, response.Message ?? "Sin mensaje")); });

            // Esperar con timeout y capturar tanto éxito como mensaje de error
            var timeoutTask = Task.Delay(10000); // Aumentar timeout a 10 segundos
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
    }
}
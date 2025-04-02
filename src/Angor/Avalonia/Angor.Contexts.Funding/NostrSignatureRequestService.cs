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

namespace Angor.Contexts.Funding;

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
            var wordsResult = await seedwordsProvider.GetSensitiveData(walletId);
            if (wordsResult.IsFailure)
            {
                return Result.Failure($"Error al recuperar datos sensibles: {wordsResult.Error}");
            }

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

            var signRequest = new SignRecoveryRequest
            {
                ProjectIdentifier = projectId.Value,
                TransactionHex = signedTransaction.Transaction.ToHex()
            };

            // Serializar
            var serialized = serializer.Serialize(signRequest);

            // Encriptar según NIP-04 (fundamental para compatibilidad con Nostr)
            var encryptedContent = await encryptionService.EncryptNostrContentAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                serialized);

            var tcs = new TaskCompletionSource<(bool Success, string Message)>();

            relayService.SendDirectMessagesForPubKeyAsync(
                senderPrivateKeyHex,
                nostrPubKey,
                encryptedContent, // Usar el contenido encriptado
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
    }
}
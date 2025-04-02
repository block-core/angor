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
            // 1. Obtener las palabras (semilla) de la billetera
            var wordsResult = await seedwordsProvider.GetSensitiveData(walletId);
            if (wordsResult.IsFailure)
            {
                return Result.Failure($"Error while retrieving sensitive data: {wordsResult.Error}");
            }

            var words = wordsResult.Value.ToWalletWords();
            
            // 2. Derivar claves para Nostr
            var senderPubKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
            var senderPrivateKey = derivationOperations.DeriveNostrStorageKey(words);
            var senderPrivateKeyHex = Encoders.Hex.EncodeData(senderPrivateKey.ToBytes());
            
            // 3. Crear la solicitud de firma
            var signRequest = new SignRecoveryRequest
            {
                ProjectIdentifier = projectId.Value,
                TransactionHex = signedTransaction.Transaction.ToHex()
            };

            // 4. Serializar la solicitud
            var serialized = serializer.Serialize(signRequest);
            
            // 5. Enviar mensaje directo al fundador
            bool requestSent = false;
            relayService.SendDirectMessagesForPubKeyAsync(
                senderPrivateKeyHex,
                founderPubKey,
                serialized,
                x => {
                    requestSent = x.Accepted;
                });

            // 6. Esperar un tiempo razonable para que se procese el envío
            await Task.Delay(500);
            
            return requestSent 
                ? Result.Success() 
                : Result.Failure("No se pudo enviar la solicitud de firmas al fundador");
        }
        catch (Exception ex) {
            return Result.Failure($"Error al enviar solicitud de firma: {ex.Message}");
        }
    }
}
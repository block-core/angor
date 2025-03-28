using Angor.Client.Models;
using Angor.Client.Services;
using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using Nostr.Client.Messages;

namespace Angor.Projects.Infrastructure.Impl;

public class InvestmentRepository(IDerivationOperations derivationOperations,
    IEncryptionService encryption,
    ISerializer serializer,
    IRelayService relayService) : IInvestmentRepository
{
    public async Task<Result> Save(Investment investment)
    {
        return Result.Success();
    }

    public async Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId)
    {
        return Result.Success<IEnumerable<Investment>>(new List<Investment>());
    }

    public async Task<Result<IList<InvestmentDto>>> GetByProject(ProjectId projectId)
    {
        // Obtener la wallet
        var words = new WalletWords()
        {
            Words = "print foil moment average quarter keep amateur shell tray roof acoustic where",
            Passphrase = "",
        };

        // Derivar claves para Nostr
        var storageKey = derivationOperations.DeriveNostrStorageKey(words);
        var storageKeyHex = Encoders.Hex.EncodeData(storageKey.ToBytes());
        var password = derivationOperations.DeriveNostrStoragePassword(words);

        // Recuperar mensajes cifrados de Nostr
        var nostrMessgesResult = await GetNostrMessages(storageKeyHex);
        if (nostrMessgesResult.IsFailure)
        {
            return Result.Failure<IList<InvestmentDto>>(nostrMessgesResult.Error);
        }

        var messages = nostrMessgesResult.Value;

        var tasks = messages.Select(e => ToInvestmentDtos(e, password));
        var investments = await Task.WhenAll(tasks);

        return investments;
    }

    private async Task<InvestmentDto> ToInvestmentDtos(NostrEvent nostrEvent, string password)
    {
        // Descifrar el contenido del mensaje
        var decryptedData = await encryption.DecryptData(nostrEvent.Content, password);
        var investmentsData = serializer.Deserialize<Investments>(decryptedData);
        return new InvestmentDto();
    }

    private Task<Result<List<NostrEvent>>> GetNostrMessages(string storageKeyHex)
    {
        return Result.Try(async () =>
        {
            var events = new List<NostrEvent>();

            await relayService.LookupDirectMessagesForPubKeyAsync(
                nostrPubKey: storageKeyHex,
                since: null,
                limit: null,
                onResponseAction: nostrEvent =>
                {
                    events.Add(item: nostrEvent);
                    return Task.CompletedTask;
                },
                fromNpub: null
            );

            return events;
        });
    }
}
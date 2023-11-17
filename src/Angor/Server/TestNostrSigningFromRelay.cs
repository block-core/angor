using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Angor.Shared.Utilities;
using Newtonsoft.Json;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Direct;
using Nostr.Client.Requests;

namespace Angor.Server;

public class TestNostrSigningFromRelay : ITestNostrSigningFromRelay 
{
    private static NostrWebsocketClient? _nostrClient;
    private static INostrCommunicator? _nostrCommunicator;
    private ILogger<NostrWebsocketClient> _clientLogger; 
    private ILogger<NostrWebsocketCommunicator> _communicatorLogger;
    private readonly TestStorageService _storage;
    private readonly IFounderTransactionActions _founderTransactionActions;
    private readonly IInvestorTransactionActions _investorTransactionActions;
    private readonly INetworkConfiguration _networkConfiguration;
    private ILogger<TestNostrSigningFromRelay> _logger;

    string angorRootKey =
        "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";

    public TestNostrSigningFromRelay(ILogger<NostrWebsocketClient> clientLogger, ILogger<NostrWebsocketCommunicator> communicatorLogger, TestStorageService storage, IFounderTransactionActions founderTransactionActions, IInvestorTransactionActions investorTransactionActions, INetworkConfiguration networkConfiguration, ILogger<TestNostrSigningFromRelay> logger)
    {
        _clientLogger = clientLogger;
        _communicatorLogger = communicatorLogger;
        _storage = storage;
        _founderTransactionActions = founderTransactionActions;
        _investorTransactionActions = investorTransactionActions;
        _networkConfiguration = networkConfiguration;
        _logger = logger;
    }

    private void SetupNostrClient()
        {
            _nostrClient = new NostrWebsocketClient(_nostrCommunicator, _clientLogger);
            
            _nostrClient.Streams.UnknownMessageStream.Subscribe(_ => _clientLogger.LogError($"UnknownMessageStream {_.MessageType} {_.AdditionalData}"));
            _nostrClient.Streams.EventStream.Subscribe(_ => _clientLogger.LogInformation($"EventStream {_.Subscription} {_.AdditionalData}"));
            _nostrClient.Streams.NoticeStream.Subscribe(_ => _clientLogger.LogError($"NoticeStream {_.Message}"));
            _nostrClient.Streams.UnknownRawStream.Subscribe(_ => _clientLogger.LogError($"UnknownRawStream {_.Message}"));
            
            _nostrClient.Streams.OkStream.Subscribe(_ =>
            {
                _clientLogger.LogInformation($"OkStream {_.Accepted} message - {_.Message}");
            });

            _nostrClient.Streams.EoseStream.Subscribe(_ =>
            {
                _clientLogger.LogInformation($"EoseStream {_.Subscription} message - {_.AdditionalData}");
            });
        }

    private void SetupNostrCommunicator()
    {
        _nostrCommunicator = new NostrWebsocketCommunicator(new Uri("wss://relay.angor.io"))
        {
            Name = "angor-relay.test",
            ReconnectTimeout = null //TODO need to check what is the actual best time to set here
        };

        _nostrCommunicator.DisconnectionHappened.Subscribe(info =>
        {
            if (info.Exception != null)
                _communicatorLogger.LogError(info.Exception,
                    "Relay disconnected, type: {Type}, reason: {CloseStatus}", info.Type,
                    info.CloseStatusDescription);
            else
                _communicatorLogger.LogInformation("Relay disconnected, type: {Type}, reason: {CloseStatus}",
                    info.Type, info.CloseStatusDescription);
        });

        _nostrCommunicator.MessageReceived.Subscribe(info =>
        {
            _communicatorLogger.LogInformation(
                "message received on communicator - {Text} Relay message received, type: {MessageType}",
                info.Text, info.MessageType);
        });
    }

    public async Task SignTransactionsFromNostrAsync(string projectIdentifier)
    {
        var projectKeys = await _storage.GetKeys(projectIdentifier);

        SetupNostrCommunicator();
        SetupNostrClient();
        await _nostrCommunicator.StartOrFail();

        var nostrPrivateKey = NostrPrivateKey.FromHex(projectKeys.nostrPrivateKey);
        var nostrPubKey = nostrPrivateKey.DerivePublicKey().Hex;
        
        _nostrClient.Streams.EventStream.Where(_ => _.Subscription == nostrPubKey + "1")
            .Where(_ => _.Event.Kind == NostrKind.ApplicationSpecificData)
            .Subscribe(_ =>
            {
                _clientLogger.LogInformation("application specific data" + _.Event.Content);
                var data = System.Text.Json.JsonSerializer.Deserialize<ProjectInfo>(_.Event.Content, settings);
                _storage.Add(data);
            });

        _nostrClient.Streams.EventStream.Where(_ => _.Subscription == nostrPubKey + "2")
            .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
            .Select(_ => _.Event as NostrEncryptedEvent)
            .Subscribe(nostrEvent =>
            {
                SignInvestorTransactionsAsync(projectIdentifier, nostrEvent, nostrPrivateKey, projectKeys);
            });
        
        _nostrClient.Send(new NostrRequest( nostrPubKey + "1", new NostrFilter
        {
            Authors = new []{nostrPubKey},
            Kinds = new[] { NostrKind.ApplicationSpecificData },
            Limit = 1
        }));

        _nostrClient.Send(new NostrRequest(nostrPubKey + "2", new NostrFilter
        {
            P = new[] { nostrPubKey },
            Kinds = new[] { NostrKind.EncryptedDm },
            Since = DateTime.UtcNow
        }));
    }

    private void SignInvestorTransactionsAsync(string projectIdentifier, NostrEncryptedEvent? nostrEvent,
        NostrPrivateKey nostrPrivateKey, ProjectKeys projectKeys)
    {
        _clientLogger.LogInformation("encrypted direct message");
        var project = (_storage.Get().GetAwaiter().GetResult()).First(_ => _.ProjectIdentifier == projectIdentifier);
        var transactionHex = nostrEvent.DecryptContent(nostrPrivateKey);

        _clientLogger.LogInformation(transactionHex);

        var sig = signProject(transactionHex, project, projectKeys.founderSigningPrivateKey);
        
        foreach (var stage in sig.Signatures)
        {
            var sigJson = System.Text.Json.JsonSerializer.Serialize(stage.Signature);

            _logger.LogInformation($"Signature to send for stage {stage.StageIndex}: {sigJson}");

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = sigJson,
                Tags = new NostrEventTags(new[] { NostrEventTag.Profile(nostrEvent.Pubkey) })
            };

            var signed = NostrEncryptedEvent.EncryptDirectMessage(ev, nostrPrivateKey)
                .Sign(nostrPrivateKey);

            _nostrClient.Send(new NostrEventRequest(signed));
        }
    }

    private SignatureInfo signProject(string transactionHex,ProjectInfo info, string founderSigningPrivateKey)
    {
        var investorTrx = _networkConfiguration.GetNetwork().CreateTransaction(transactionHex);

        // build sigs
        var recoverytrx = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(info, investorTrx);
        var sig = _founderTransactionActions.SignInvestorRecoveryTransactions(info, transactionHex, recoverytrx, founderSigningPrivateKey);

        if (!_investorTransactionActions.CheckInvestorRecoverySignatures(info, investorTrx, sig))
            throw new InvalidOperationException();

        return sig;
    }

    private JsonSerializerOptions settings => new()
    {
        // Equivalent to Formatting = Formatting.None
        WriteIndented = false,

        // Equivalent to NullValueHandling = NullValueHandling.Ignore
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

        // PropertyNamingPolicy equivalent to CamelCasePropertyNamesContractResolver
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        Converters = { new UnixDateTimeConverter() }
    };
}

public interface ITestNostrSigningFromRelay
{
    public Task SignTransactionsFromNostrAsync(string projectIdentifier);
}



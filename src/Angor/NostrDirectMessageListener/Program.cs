using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.NBitcoin.Protocol;
using Angor.Client.Services;
using Angor.Shared;
using Angor.Shared.Models; 
using Blockcore.Networks; 	
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Angor.Client.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Angor.Client;
using Blazored.LocalStorage;
using Microsoft.JSInterop;
using Angor.Client.Models;
using Angor.Shared;
using Angor.Shared.Models;
using System.Text.Json.Serialization;
using Nostr.Client;
using Nostr.Client.Messages;
using Nostr.Client.Requests; 
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;

class Program
{
    private const string RelayUrl = "wss://relay.angor.io/"; // Replace with your relay
    private const string RecipientPrivateKey = "362f4b51ecac1bc07a3216d9b5da1abfcb42f04f51ebfd659d5ebfc21f62b05d"; // Your private key
    private const string RecipientPublicKey = "b61f43c4a88d538ee1e74979b75c4d54fd5ff756923f14aef0366bdab9b3cbcc"; // Corresponding public key
private const string SettingsFilePath = "settings.json";

private static ISignService _signService;
    private readonly INetworkConfiguration _networkConfiguration;
private readonly INostrCommunicationFactory _communicationFactory;
private readonly INetworkService _networkService;
private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
private readonly ISeederScriptTreeBuilder _seederScriptTreeBuilder;
private readonly IProjectScriptsBuilder _projectScriptsBuilder;
private readonly IHdOperations _hdOperations;
private readonly ISpendingTransactionBuilder _spendingTransactionBuilder;
private readonly IInvestmentTransactionBuilder _investmentTransactionBuilder;
private readonly ITaprootScriptBuilder _taprootScriptBuilder;
private static IEncryptionService encryption;
private static  IInvestorTransactionActions InvestorTransactionActions;
private static  IFounderTransactionActions FounderTransactionActions;
private static IDerivationOperations DerivationOperations;
private static IWalletStorage _walletStorage = new WalletStorage();
private static ISerializer serializer;

private static IIndexerService _IndexerService; 
private static IRelayService RelayService;

public Program(
    ISignService signService,
    INetworkConfiguration networkConfiguration,
    INostrCommunicationFactory communicationFactory,
    INetworkService networkService,
    IEncryptionService encryptionPass,
	IInvestorTransactionActions investorTransactionActions,
	IFounderTransactionActions founderTransactionActions,
	IDerivationOperations derivationOperations,
	    IWalletStorage walletStorage,
	    ISerializer _serializer,
	    IIndexerService indexerService,
	    IRelayService relayService,
		IInvestmentScriptBuilder investmentScriptBuilder,
		ISeederScriptTreeBuilder seederScriptTreeBuilder, 
		IProjectScriptsBuilder  projectScriptsBuilder, 
IHdOperations 	    hdOperations,
ISpendingTransactionBuilder spendingTransactionBuilder,
IInvestmentTransactionBuilder investmentTransactionBuilder,
ITaprootScriptBuilder taprootScriptBuilder)
{
    _signService = signService ?? throw new ArgumentNullException(nameof(signService));
    _networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
    _communicationFactory = communicationFactory ?? throw new ArgumentNullException(nameof(communicationFactory));
    _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
    encryption = encryptionPass ?? throw new ArgumentNullException(nameof(encryptionPass));
	InvestorTransactionActions = investorTransactionActions ?? throw new ArgumentNullException(nameof(investorTransactionActions));
	FounderTransactionActions = founderTransactionActions ?? throw new ArgumentNullException(nameof(founderTransactionActions));
	DerivationOperations = derivationOperations ?? throw new ArgumentNullException(nameof(derivationOperations));
	_walletStorage = walletStorage ?? throw new ArgumentNullException(nameof(walletStorage));
	serializer = _serializer ?? throw new ArgumentNullException(nameof(serializer));
	_IndexerService = indexerService ?? throw new ArgumentNullException(nameof(indexerService));
	RelayService = relayService ?? throw new ArgumentNullException(nameof(relayService));
	_investmentScriptBuilder = investmentScriptBuilder ?? throw new ArgumentNullException(nameof(investmentScriptBuilder));
	_seederScriptTreeBuilder = seederScriptTreeBuilder ?? throw new ArgumentNullException(nameof(seederScriptTreeBuilder));
	_projectScriptsBuilder = projectScriptsBuilder ?? throw new ArgumentNullException(nameof(projectScriptsBuilder));
	_hdOperations = hdOperations ?? throw new ArgumentNullException(nameof(hdOperations));
	_spendingTransactionBuilder = spendingTransactionBuilder ?? throw new ArgumentNullException(nameof(spendingTransactionBuilder));
   	
_taprootScriptBuilder = taprootScriptBuilder ?? throw new ArgumentNullException(nameof(taprootScriptBuilder));
	    _investmentTransactionBuilder = investmentTransactionBuilder ?? throw new ArgumentNullException(nameof(investmentTransactionBuilder));
}




    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var program = host.Services.GetRequiredService<Program>();
        await program.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((_, services) =>
        {
            services.AddScoped<ISignService, SignService>();
            services.AddSingleton<INetworkService, NetworkService>();
            services.AddSingleton<INetworkConfiguration, NetworkConfiguration>();
            services.AddSingleton<INostrCommunicationFactory, NostrCommunicationFactory>();
            services.AddSingleton<INetworkStorage, ClientStorage>(); // Register ClientStorage for INetworkStorage
            services.AddSingleton<IClientStorage, ClientStorage>(); // Register ClientStorage for IClientStorage
services.AddSingleton<IRelaySubscriptionsHandling, RelaySubscriptionsHandling>();
services.AddSingleton<IRelayService, RelayService>();
services.AddSingleton<IIndexerService, IndexerService>();
services.AddSingleton<ISerializer, Serializer>();
services.AddSingleton<IWalletStorage, WalletStorage>();
services.AddSingleton<IInvestmentScriptBuilder, InvestmentScriptBuilder>();
services.AddSingleton<ISeederScriptTreeBuilder, SeederScriptTreeBuilder>();
services.AddSingleton<IProjectScriptsBuilder, ProjectScriptsBuilder>();
services.AddSingleton<IHdOperations, HdOperations>();
services.AddSingleton<ISpendingTransactionBuilder, SpendingTransactionBuilder>();
services.AddSingleton<IInvestmentTransactionBuilder, InvestmentTransactionBuilder>();
services.AddSingleton<ITaprootScriptBuilder, TaprootScriptBuilder>();
services.AddSingleton<IInvestorTransactionActions, InvestorTransactionActions>();
services.AddSingleton<IFounderTransactionActions, FounderTransactionActions>();
services.AddSingleton<IDerivationOperations, DerivationOperations>();
services.AddScoped<IEncryptionService>(provider =>
{
    var jsRuntime = provider.GetService<IJSRuntime>();
    var encryptionService = new EncryptionService(jsRuntime);

    // Assume that JSRuntime is available only in Blazor contexts
    bool isJsRuntimeAvailable = jsRuntime != null;

    return new EncryptionServiceWrapper(encryptionService, isJsRuntimeAvailable);
});
services.AddBlazoredLocalStorage();
            services.AddLogging();
            services.AddHttpClient(); // Adds HttpClient support
            services.AddSingleton<Program>();
        });





    public async Task RunAsync()
{
    Console.WriteLine("Initializing application...");

    if (!_walletStorage.HasWallet())
    {
        Console.WriteLine("[INFO] No wallet found. Creating a new wallet with hard-coded words...");
        CreateAndSaveHardCodedWallet();
    }

    var network = Networks.Bitcoin.Testnet();
    Console.WriteLine($"[INFO] Setting network to: {network.Name} ({network.NetworkType})");
    _networkConfiguration.SetNetwork(network);

    try
    {
        network = _networkConfiguration.GetNetwork();
        Console.WriteLine($"[INFO] Network in use: {network.Name}");
        Console.WriteLine($"[INFO] Network type in use: {network.NetworkType}");

        var indexerUrl = _networkConfiguration.GetIndexerUrl();
        Console.WriteLine($"[INFO] Indexer URL in use: {indexerUrl.Url}");

        using var client = new ClientWebSocket();
        Console.WriteLine("Connecting to relay...");
        await client.ConnectAsync(new Uri(RelayUrl), CancellationToken.None);
        Console.WriteLine("Connected to relay.");

        var storage = new ClientStorage();
        var founderProjects = new List<FounderProject>();

        Console.WriteLine("Looking up founder projects...");
        await LookupProjectKeysOnIndexerAsync(_walletStorage, _IndexerService, RelayService, serializer, storage, founderProjects);
        Console.WriteLine("Finished looking up founder projects.");

        Console.WriteLine("Listening for messages...");
        await ListenForMessages(client);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] An error occurred: {ex.Message}");
    }
    finally
    {
        Console.WriteLine("Application exiting...");
    }
}



private void CreateAndSaveHardCodedWallet()
{
    var hardCodedWords = "custom multiply swarm wish salmon coast face foam alert matrix recycle inspire";

    var founderKeys = new FounderKeyCollection
{
    Keys = new List<FounderKeys>
    {
        new FounderKeys { NostrPubKey = "pubkey1", ProjectIdentifier = "project1" },
        new FounderKeys { NostrPubKey = "pubkey2", ProjectIdentifier = "project2" }
    }
};


    // Create and save the wallet
    var wallet = new Wallet
    {
        FounderKeys = founderKeys,
        //EncryptedData = EncryptMnemonic(hardCodedWords) // Optional: Encrypt the mnemonic if necessary
    };

    _walletStorage.SaveWalletWords(wallet);
    Console.WriteLine("[INFO] Hard-coded wallet created and saved.");
}

private string LoadIndexerUrlFromSettings()
{
    if (!File.Exists(SettingsFilePath))
        return null;

    var settingsJson = File.ReadAllText(SettingsFilePath);
    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);
    return settings != null && settings.ContainsKey("IndexerUrl") ? settings["IndexerUrl"] : null;
}

private void SaveIndexerUrlToSettings(string url)
{
    Dictionary<string, string> settings;

    // Load existing settings
    if (File.Exists(SettingsFilePath))
    {
        var settingsJson = File.ReadAllText(SettingsFilePath);
        settings = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson) ?? new Dictionary<string, string>();
    }
    else
    {
        settings = new Dictionary<string, string>();
    }

    // Update indexer URL
    settings["IndexerUrl"] = url;

    // Save updated settings
    var updatedSettingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(SettingsFilePath, updatedSettingsJson);
    Console.WriteLine($"[INFO] Saved indexer URL to settings: {url}");
}



   private static async Task SubscribeToEncryptedDM(ClientWebSocket client, string publicKey)
{
    if (client == null)
    {
        Console.WriteLine("[ERROR] WebSocket client is null.");
        throw new ArgumentNullException(nameof(client));
    }

    if (string.IsNullOrEmpty(publicKey))
    {
        Console.WriteLine("[ERROR] Public key is null or empty.");
        throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));
    }

    string subscriptionId = Guid.NewGuid().ToString();
    string subscriptionMessage = $@"
    [
        ""REQ"",
        ""{subscriptionId}"",
        {{
            ""kinds"": [4],
            ""#p"": [""{publicKey}""]
        }}
    ]".Trim();

    Console.WriteLine($"[DEBUG] Subscription ID: {subscriptionId}");
    Console.WriteLine($"[DEBUG] Subscription JSON: {subscriptionMessage}");

    try
    {
        if (client.State != WebSocketState.Open)
        {
            Console.WriteLine($"[ERROR] WebSocket is not open. Current state: {client.State}");
            return;
        }

        Console.WriteLine("[DEBUG] Sending subscription message...");
        await client.SendAsync(
            Encoding.UTF8.GetBytes(subscriptionMessage),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );

        Console.WriteLine("[DEBUG] Subscription message sent successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to send subscription message: {ex.Message}");
    }
}





    private static async Task ListenForMessages(ClientWebSocket client)
{
    var buffer = new byte[1024 * 64]; // 64 KB buffer

    while (client.State == WebSocketState.Open)
    {
        try
        {
            Console.WriteLine("[DEBUG] Waiting to receive a message...");
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"[DEBUG] Message received (length: {result.Count}): {message}");

                // Process the message
                ProcessNostrEvent(message, client);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("[INFO] WebSocket connection closed by server.");
                break;
            }
            else
            {
                Console.WriteLine($"[WARNING] Unsupported WebSocket message type: {result.MessageType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error while receiving message: {ex.Message}");
            break;
        }
    }

    Console.WriteLine("[INFO] Exiting ListenForMessages loop.");
}


   private static async void HandleEventMessage(
    object[] response, 
    ClientWebSocket client, 
    ISignService signService, 
    IEncryptionService encryptionService)
{
    try
    {
        if (response.Length > 2 && response[2] is JsonElement eventData)
        {
            Console.WriteLine($"Raw event data: {eventData}");

            if (JsonSerializer.Deserialize<NostrEvent>(eventData.GetRawText()) is not { } nostrEvent)
            {
                Console.WriteLine("Deserialized NostrEvent is null.");
                return;
            }

            Console.WriteLine($"NostrEvent: {nostrEvent}");
            Console.WriteLine($"NostrEvent Kind: {nostrEvent.Kind}");



            if (nostrEvent.Kind == 4 && !string.IsNullOrEmpty(nostrEvent.Content))
            {
				string investorPubKey = nostrEvent.Pubkey;
                string eventId = nostrEvent.Id;
				var decryptedContent = await DecryptMessageAsync(encryption, RecipientPrivateKey, investorPubKey, nostrEvent.Content);
                if (!string.IsNullOrEmpty(decryptedContent))
                {
                    Console.WriteLine($"Decrypted message: {decryptedContent}");

                    // Use encryptionService
					//string signedTransactionHex = BitcoinUtils.DecodeAndSignTransaction(decryptedContent, RecipientPrivateKey);
                    //var key = DerivationOperations.DeriveFounderRecoveryPrivateKey(words, FounderProject.ProjectIndex); // TODO add this later
                    string encryptedContent = await encryptionService.EncryptNostrContentAsync(RecipientPrivateKey, investorPubKey, nostrEvent.Content);

                    Console.WriteLine($"[DEBUG] Encrypted Content: {encryptedContent}");
					Console.WriteLine($"[DEBUG] Recipient Private Key: {RecipientPrivateKey}");
                    Console.WriteLine($"[DEBUG] Investor Public Key: {investorPubKey}");
                    Console.WriteLine($"[DEBUG] Event ID: {eventId}");

                    if (!string.IsNullOrEmpty(encryptedContent))
                    {
                        Console.WriteLine($"Encrypted Content: {encryptedContent}");

                        // Call SendSignaturesToInvestor
                        DateTime sentTimestamp = signService.SendSignaturesToInvestor(
                            encryptedContent,
                            RecipientPrivateKey,
                            nostrEvent.Pubkey,
                            nostrEvent.Id
                        );

                        Console.WriteLine($"[INFO] Signatures sent successfully at: {sentTimestamp}");
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error handling EVENT message: {ex.Message}");
    }
}







private static void ProcessNostrEvent(string message, ClientWebSocket client)
{
    try
    {
        Console.WriteLine($"[DEBUG] Processing message: {message}");

        if (message.StartsWith("["))
        {
            var response = JsonSerializer.Deserialize<object[]>(message);
            if (response != null && response.Length > 0)
            {
                string eventType = response[0]?.ToString();
                Console.WriteLine($"[DEBUG] Parsed event type: {eventType}");

                if (eventType == "EVENT")
                {
				HandleEventMessage(response, client, _signService, encryption);
                }
                else
                {
                    Console.WriteLine($"[INFO] Received non-EVENT message: {eventType}");
                }
            }
            else
            {
                Console.WriteLine("[WARNING] Malformed or empty response received.");
            }
        }
        else
        {
            Console.WriteLine("[INFO] Non-JSON message received.");
        }
    }
    catch (JsonException jsonEx)
    {
        Console.WriteLine($"[ERROR] JSON parsing error: {jsonEx.Message}");
        Console.WriteLine($"[DEBUG] Raw message: {message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Unexpected error processing message: {ex.Message}");
        Console.WriteLine($"[DEBUG] Raw message: {message}");
    }
}

   


string ExtractInvestorPubKeyFromEvent(NostrEvent nostrEvent)
{
    var investorTag = nostrEvent.Tags.FirstOrDefault(tag => tag.Count > 0 && tag[0] == "p");
    return investorTag != null && investorTag.Count > 1 ? investorTag[1] : null;
}

string ExtractInvestorPubKeyFromDecryptedMessage(string decryptedMessage)
{
    try
    {
        var jsonDoc = JsonDocument.Parse(decryptedMessage);
        if (jsonDoc.RootElement.TryGetProperty("investorPubKey", out var pubKeyElement))
        {
            return pubKeyElement.GetString();
        }
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"[ERROR] Failed to parse decrypted message: {ex.Message}");
    }
    return null;
}

string GetInvestorPubKey(NostrEvent nostrEvent, string decryptedMessage)
{
    // Try extracting from the event tags first
var investorPubKey = ExtractInvestorPubKeyFromEvent(nostrEvent) 
                     ?? throw new ArgumentException("Investor public key is missing.");    if (!string.IsNullOrEmpty(investorPubKey))
    {
        return investorPubKey;
    }

    // Fallback to extracting from the decrypted message
    return ExtractInvestorPubKeyFromDecryptedMessage(decryptedMessage);
}




private static async Task<string> DecryptMessageAsync(IEncryptionService encryptionService, string recipientPrivateKey, string senderPublicKey, string encryptedContent)
{
    try
    {
        if (string.IsNullOrEmpty(encryptedContent))
        {
            Console.WriteLine("Encrypted content is null or empty.");
            return null;
        }

        if (encryptionService == null)
        {
            Console.WriteLine("Encryption service is null.");
            return null;
        }

        // Use the encryption service to decrypt the content
        string decryptedContent = await encryptionService.DecryptNostrContentAsync(recipientPrivateKey, senderPublicKey, encryptedContent);

        if (string.IsNullOrEmpty(decryptedContent))
        {
            Console.WriteLine("Failed to decrypt content.");
            return null;
        }

        return decryptedContent;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error decrypting message: {ex.Message}");
        return null;
    }
}



  /*  public static void SendSignaturesToInvestor(
    string encryptedSignatureInfo,
    string investorPubKey,
    string eventId,
    ISignService signService)
{
    // Use ISignService to process the event
    signService.SendSignaturesToInvestor(encryptedSignatureInfo,RecipientPrivateKey , investorPubKey, eventId);

    Console.WriteLine($"[INFO] Signatures sent successfully for Event ID: {eventId}");
}

*/
private static async Task LookupProjectKeysOnIndexerAsync(
    IWalletStorage walletStorage,
    IIndexerService indexerService,
    IRelayService relayService,
    ISerializer serializer,
    ClientStorage storage, // Use ClientStorage here
    List<FounderProject> founderProjects)
{
    Console.WriteLine("Starting lookup of founder projects...");

    try
    {
        var founderKeys = walletStorage.GetFounderKeys();
        var projectsToLookup = new Dictionary<string, ProjectIndexerData>();

        foreach (var key in founderKeys.Keys)
        {
            if (founderProjects.Any(project => project.ProjectInfo.ProjectIdentifier == key.ProjectIdentifier))
            {
                Console.WriteLine($"Project with identifier {key.ProjectIdentifier} already exists, skipping.");
                continue;
            }

            var indexerProject = await indexerService.GetProjectByIdAsync(key.ProjectIdentifier);
            if (indexerProject != null)
            {
                projectsToLookup.Add(key.NostrPubKey, indexerProject);
            }
            else
            {
                Console.WriteLine($"Project with identifier {key.ProjectIdentifier} could not be found.");
            }
        }

        if (!projectsToLookup.Any())
        {
            Console.WriteLine("No new projects to look up.");
            return;
        }

        relayService.RequestProjectCreateEventsByPubKey(
            eventResponse =>
            {
                switch (eventResponse.Kind)
                {
                    case NostrKind.Metadata:
                        ProcessMetadataEvent(eventResponse, projectsToLookup, founderProjects, serializer);
                        break;

                    case NostrKind.ApplicationSpecificData:
                        ProcessApplicationSpecificDataEvent(eventResponse, projectsToLookup, founderProjects, serializer);
                        break;



                    default:
                        Console.WriteLine($"Unsupported event kind: {eventResponse.Kind}");
                        break;
                }
            },
            () =>
            {
                Console.WriteLine("Finished processing project creation events.");

                foreach (var project in founderProjects)
                {
                    var existingProject = storage.GetFounderProjects()
                        .FirstOrDefault(p => p.ProjectInfo.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

                    if (existingProject == null)
                    {
                        storage.AddFounderProject(new[] { project });
                        Console.WriteLine($"Added new project: {project.ProjectInfo.ProjectIdentifier}");
                    }
                    else
                    {
                        storage.UpdateFounderProject(project);
                        Console.WriteLine($"Updated existing project: {project.ProjectInfo.ProjectIdentifier}");
                    }
                }
            },
            projectsToLookup.Keys.ToArray()
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during project lookup: {ex.Message}");
    }
}



private static void ProcessMetadataEvent(
    Nostr.Client.Messages.NostrEvent eventResponse,
    Dictionary<string, ProjectIndexerData> projectsToLookup,
    List<FounderProject> founderProjects,
    ISerializer serializer)
{
    var metadata = serializer.Deserialize<ProjectMetadata>(eventResponse.Content);
    var existingProject = founderProjects.FirstOrDefault(p => p.ProjectInfo.NostrPubKey == eventResponse.Pubkey);

    if (existingProject != null)
    {
        existingProject.Metadata ??= metadata;
        Console.WriteLine($"Updated metadata for project with public key: {eventResponse.Pubkey}");
    }
    else
    {
        var newProject = CreateFounderProject(projectsToLookup, eventResponse);
        newProject.Metadata = metadata;
        founderProjects.Add(newProject);
        Console.WriteLine($"Added new project with public key: {eventResponse.Pubkey}");
    }
}



private static void ProcessApplicationSpecificDataEvent(
    Nostr.Client.Messages.NostrEvent eventResponse,
    Dictionary<string, ProjectIndexerData> projectsToLookup,
    List<FounderProject> founderProjects,
    ISerializer serializer)
{
    if (!projectsToLookup.TryGetValue(eventResponse.Pubkey, out var indexerData) ||
        eventResponse.Id != indexerData.NostrEventId)
    {
        Console.WriteLine($"Event mismatch or invalid ID for public key: {eventResponse.Pubkey}");
        return;
    }

    var projectInfo = serializer.Deserialize<ProjectInfo>(eventResponse.Content);
    var existingProject = founderProjects.FirstOrDefault(p => p.ProjectInfo.NostrPubKey == eventResponse.Pubkey);

    if (existingProject != null)
    {
        if (string.IsNullOrEmpty(existingProject.ProjectInfo.ProjectIdentifier))
        {
            existingProject.ProjectInfo = projectInfo;
            Console.WriteLine($"Updated project info for public key: {eventResponse.Pubkey}");
        }
    }
    else
    {
        var newProject = CreateFounderProject(projectsToLookup, eventResponse, projectInfo);
        founderProjects.Add(newProject);
        Console.WriteLine($"Added new project with public key: {eventResponse.Pubkey}");
    }
}


private static FounderProject CreateFounderProject(
    Dictionary<string, ProjectIndexerData> projectsToLookup,
    Nostr.Client.Messages.NostrEvent eventResponse,
    ProjectInfo? projectInfo = null)
{
    var indexerData = projectsToLookup[eventResponse.Pubkey];
    return new FounderProject
    {
        ProjectInfo = projectInfo ?? new ProjectInfo
        {
            ProjectIdentifier = indexerData.ProjectIdentifier,
            NostrPubKey = eventResponse.Pubkey,
        },
        Metadata = null,
        ProjectIndex = (int)indexerData.CreatedOnBlock,
    };
}






}

// Classes and Utility Functions:
public class NostrEvent
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("kind")] public int Kind { get; set; }
    [JsonPropertyName("pubkey")] public string? Pubkey { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("created_at")] public long CreatedAt { get; set; }
    [JsonPropertyName("tags")] public List<List<string>> Tags { get; set; } = new();

    public void Sign(Key privateKey)
    {
        var serializedEvent = $"{Kind}:{CreatedAt}:{Content}";
        var hash = Blockcore.NBitcoin.Crypto.Hashes.SHA256(Encoding.UTF8.GetBytes(serializedEvent));
        Id = Encoders.Hex.EncodeData(hash);

        var signature = privateKey.Sign(new uint256(hash));
        Pubkey = privateKey.PubKey.Compress().ToHex();
    }
}

public static class BitcoinUtils
{
   public static string DecodeAndSignTransaction(string rawTransactionHex, string privateKeyHex)
{
    try
    {
//var network = Network.Bitcoin.TestNet; // Or Network.Main for mainnet
        var network = Networks.Bitcoin.Testnet();
        var consensusFactory = network.Consensus.ConsensusFactory;
        var transaction = consensusFactory.CreateTransaction(rawTransactionHex);

        var privateKey = new Key(Encoders.Hex.DecodeData(privateKeyHex));
        var builder = new TransactionBuilder(network);

        builder.AddCoins(transaction.Outputs.AsCoins());
        builder.AddKeys(privateKey);

        var signedTransaction = builder.SignTransaction(transaction);
        return signedTransaction.ToHex();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error decoding or signing transaction: {ex.Message}");
        return null;
    }
}

}

public static class Configuration
{
    public static string RelayUrl => "wss://relay.angor.io/";
    public static string RecipientPrivateKey => "362f4b51ecac1bc07a3216d9b5da1abfcb42f04f51ebfd659d5ebfc21f62b05d";
    public static string RecipientPublicKey => "b61f43c4a88d538ee1e74979b75c4d54fd5ff756923f14aef0366bdab9b3cbcc";
}

public class MessageProcessor
{
    private readonly ClientWebSocket _client;
    private readonly ITransactionService _transactionService;

    public MessageProcessor(ClientWebSocket client, string recipientPrivateKey)
    {
        _client = client;
        _transactionService = new TransactionService(recipientPrivateKey);
    }

    public async Task SubscribeToMessages(string publicKey)
    {
        var subscriptionMessage = MessageUtils.CreateSubscriptionMessage(publicKey);
        await _client.SendAsync(
            Encoding.UTF8.GetBytes(subscriptionMessage),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    public async Task ListenForMessages()
    {
        var buffer = new byte[1024 * 64];
        while (_client.State == WebSocketState.Open)
        {
            var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ProcessMessage(message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("Connection closed.");
                break;
            }
        }
    }

    private void ProcessMessage(string message)
    {
        if (MessageUtils.TryParseEventMessage(message, out var nostrEvent))
        {
            _transactionService.HandleEvent(nostrEvent);
        }
        else
        {
            Console.WriteLine($"Unhandled message: {message}");
        }
    }
}

public interface ITransactionService
{
    void HandleEvent(NostrEvent nostrEvent);
}

public class TransactionService : ITransactionService
{
    private readonly string _privateKey;

    public TransactionService(string privateKey)
    {
        _privateKey = privateKey;
    }

    public void HandleEvent(NostrEvent nostrEvent)
    {
        if (nostrEvent.Kind == 4)
        {
            var decryptedContent = EncryptionUtils.DecryptMessage(
                _privateKey, 
                nostrEvent.Content, 
                nostrEvent.Pubkey
            );

            var signedTransactionHex = BitcoinUtils.DecodeAndSignTransaction(
                decryptedContent, 
                _privateKey
            );

            Console.WriteLine($"Signed Transaction: {signedTransactionHex}");
        }
    }
}

public static class MessageUtils
{
    public static string CreateSubscriptionMessage(string publicKey)
    {
        var subscriptionId = Guid.NewGuid().ToString();
        return $@"
        [
            ""REQ"",
            ""{subscriptionId}"",
            {{
                ""kinds"": [4],
                ""#p"": [""{publicKey}""]
            }}
        ]";
    }

    public static bool TryParseEventMessage(string message, out NostrEvent nostrEvent)
    {
        try
        {
            nostrEvent = JsonSerializer.Deserialize<NostrEvent>(message);
            return nostrEvent != null;
        }
        catch
        {
            nostrEvent = null;
            return false;
        }
    }
}

public static class EncryptionUtils
{
    public static string DecryptMessage(string recipientPrivateKey, string encryptedContent, string senderPublicKey)
    {
        try
        {
            // Split the content into ciphertext and IV
            var parts = encryptedContent.Split("?iv=");
            if (parts.Length != 2)
            {
                Console.WriteLine("Invalid encrypted content format.");
                return null;
            }

            var cipherText = Convert.FromBase64String(parts[0]);
            var iv = Convert.FromBase64String(parts[1]);

            // Derive the shared secret
            string sharedSecretHex = GetSharedSecretHexWithoutPrefix(recipientPrivateKey, senderPublicKey);

            // Decrypt the message
            using var aes = Aes.Create();
            aes.Key = Convert.FromHexString(sharedSecretHex);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decrypting message: {ex.Message}");
            return null;
        }
    }

    private static string GetSharedSecretHexWithoutPrefix(string recipientPrivateKeyHex, string senderPublicKeyHex)
    {
        try
        {
            var privateKey = new Blockcore.NBitcoin.Key(
                Blockcore.NBitcoin.DataEncoders.Encoders.Hex.DecodeData(recipientPrivateKeyHex));

            // Try with '02' prefix first
            var publicKey = new Blockcore.NBitcoin.PubKey("02" + senderPublicKeyHex);

            // Compute the shared secret
            var sharedSecret = publicKey.GetSharedPubkey(privateKey);
            return Blockcore.NBitcoin.DataEncoders.Encoders.Hex.EncodeData(sharedSecret.ToBytes()[1..]);
        }
        catch
        {
            // Fallback to '03' prefix if '02' fails
            var privateKey = new Blockcore.NBitcoin.Key(
                Blockcore.NBitcoin.DataEncoders.Encoders.Hex.DecodeData(recipientPrivateKeyHex));
            var publicKey = new Blockcore.NBitcoin.PubKey("03" + senderPublicKeyHex);

            var sharedSecret = publicKey.GetSharedPubkey(privateKey);
            return Blockcore.NBitcoin.DataEncoders.Encoders.Hex.EncodeData(sharedSecret.ToBytes()[1..]);
        }
    }



}



public class ClientStorage : IClientStorage, INetworkStorage
{
    private const string CurrencyDisplaySettingKey = "currencyDisplaySetting";
    private const string StorageFilePath = "localstorage.json";
    private readonly Dictionary<string, object> _storage;

    public ClientStorage()
    {
        if (File.Exists(StorageFilePath))
        {
            var json = File.ReadAllText(StorageFilePath);
            _storage = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
        }
        else
        {
            _storage = new Dictionary<string, object>();
        }
    }

    private void SaveChanges()
    {
        var json = JsonSerializer.Serialize(_storage);
        File.WriteAllText(StorageFilePath, json);
    }

    private T? GetItem<T>(string key)
    {
        if (_storage.TryGetValue(key, out var value) && value is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    private void SetItem<T>(string key, T value)
    {
        _storage[key] = JsonSerializer.Serialize(value);
        SaveChanges();
    }

    private void RemoveItem(string key)
    {
        if (_storage.ContainsKey(key))
        {
            _storage.Remove(key);
            SaveChanges();
        }
    }

    private void ClearStorage()
    {
        _storage.Clear();
        SaveChanges();
    }

    public AccountInfo GetAccountInfo(string network)
    {
        return GetItem<AccountInfo>(string.Format("utxo:{0}", network));
    }

    public void SetAccountInfo(string network, AccountInfo items)
    {
        SetItem(string.Format("utxo:{0}", network), items);
    }

    public void DeleteAccountInfo(string network)
    {
        RemoveItem(string.Format("utxo:{0}", network));
    }

    public void AddInvestmentProject(InvestorProject project)
    {
        var projects = GetInvestmentProjects();

        if (projects.Any(a => a.ProjectInfo?.ProjectIdentifier == project.ProjectInfo?.ProjectIdentifier))
            return;

        projects.Add(project);
        SetItem("projects", projects);
    }

    public void UpdateInvestmentProject(InvestorProject project)
    {
        var projects = GetInvestmentProjects();

        var existing = projects.FirstOrDefault(a => a.ProjectInfo?.ProjectIdentifier == project.ProjectInfo?.ProjectIdentifier);
        if (existing != null)
        {
            projects.Remove(existing);
        }

        projects.Add(project);
        SetItem("projects", projects);
    }

    public void RemoveInvestmentProject(string projectId)
    {
        var projects = GetInvestmentProjects();
        var project = projects.FirstOrDefault(a => a.ProjectInfo?.ProjectIdentifier == projectId);

        if (project != null)
        {
            projects.Remove(project);
            SetItem("projects", projects);
        }
    }

    public List<InvestorProject> GetInvestmentProjects()
    {
        return GetItem<List<InvestorProject>>("projects") ?? new List<InvestorProject>();
    }

    public void AddFounderProject(params FounderProject[] projects)
    {
        var existingProjects = GetFounderProjects();
        existingProjects.AddRange(projects);
        SetItem("founder-projects", existingProjects.OrderBy(p => p.ProjectIndex).ToList());
    }

    public List<FounderProject> GetFounderProjects()
    {
        return GetItem<List<FounderProject>>("founder-projects") ?? new List<FounderProject>();
    }

    public FounderProject? GetFounderProjects(string projectIdentifier)
    {
        var projects = GetFounderProjects();
        return projects.FirstOrDefault(p => p.ProjectInfo?.ProjectIdentifier == projectIdentifier);
    }

    public void UpdateFounderProject(FounderProject project)
    {
        var projects = GetFounderProjects();

        var existingProject = projects.FirstOrDefault(p => p.ProjectInfo?.ProjectIdentifier == project.ProjectInfo?.ProjectIdentifier);
        if (existingProject != null)
        {
            projects.Remove(existingProject);
        }

        projects.Add(project);
        SetItem("founder-projects", projects.OrderBy(p => p.ProjectIndex).ToList());
    }

    public void DeleteFounderProjects()
    {
        RemoveItem("founder-projects");
    }

    public SettingsInfo GetSettingsInfo()
    {
        return GetItem<SettingsInfo>("settings-info") ?? new SettingsInfo();
    }

    public void SetSettingsInfo(SettingsInfo settingsInfo)
    {
        SetItem("settings-info", settingsInfo);
    }

    public void WipeStorage()
    {
        ClearStorage();
    }

    public void SetNostrPublicKeyPerProject(string projectId, string nostrPubKey)
    {
        SetItem($"project:{projectId}:nostrKey", nostrPubKey);
    }

    public string GetNostrPublicKeyPerProject(string projectId)
    {
        return GetItem<string>($"project:{projectId}:nostrKey") ?? string.Empty;
    }

    public string GetCurrencyDisplaySetting()
    {
        return GetItem<string>(CurrencyDisplaySettingKey) ?? "BTC";
    }

    public void SetCurrencyDisplaySetting(string setting)
    {
        SetItem(CurrencyDisplaySettingKey, setting);
    }

    public SettingsInfo GetSettings()
    {
        return GetSettingsInfo();
    }

    public void SetSettings(SettingsInfo settingsInfo)
    {
        SetSettingsInfo(settingsInfo);
    }

    public void SetNetwork(string network)
    {
        SetItem("network", network);
    }

    public string GetNetwork()
    {
        return GetItem<string>("network") ?? string.Empty;
    }

    public void DeleteInvestmentProjects()
    {
        RemoveItem("projects");
    }

    public void AddOrUpdateSignatures(SignatureInfo signatureInfo)
    {
        var signatures = GetSignatures();
        var existing = signatures.FirstOrDefault(s => s.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (existing != null)
        {
            signatures.Remove(existing);
        }

        signatures.Add(signatureInfo);
        SetItem("recovery-signatures", signatures);
    }

    public List<SignatureInfo> GetSignatures()
    {
        return GetItem<List<SignatureInfo>>("recovery-signatures") ?? new List<SignatureInfo>();
    }

    public void RemoveSignatures(SignatureInfo signatureInfo)
    {
        var signatures = GetSignatures();
        var existing = signatures.FirstOrDefault(s => s.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (existing != null)
        {
            signatures.Remove(existing);
            SetItem("recovery-signatures", signatures);
        }
    }

    public void DeleteSignatures()
    {
        var signatures = GetSignatures();
        SetItem($"recovery-signatures-{DateTime.UtcNow.Ticks}", signatures);
        RemoveItem("recovery-signatures");
    }
}

public class NostrEventRequest
{
    public NostrEvent Event { get; }

    public NostrEventRequest(NostrEvent nostrEvent)
    {
        Event = nostrEvent;
    }
}



public class EncryptionServiceWrapper : IEncryptionService
{
    private readonly EncryptionService _encryptionService;
    private readonly bool _isJsRuntimeAvailable;

    public EncryptionServiceWrapper(EncryptionService encryptionService, bool isJsRuntimeAvailable = true)
    {
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _isJsRuntimeAvailable = isJsRuntimeAvailable;
    }

    public async Task<string> EncryptData(string secretData, string password)
    {
        if (_isJsRuntimeAvailable)
        {
            return await _encryptionService.EncryptData(secretData, password);
        }
        return FallbackEncrypt(secretData, password);
    }

    public async Task<string> DecryptData(string encryptedData, string password)
    {
        if (_isJsRuntimeAvailable)
        {
            return await _encryptionService.DecryptData(encryptedData, password);
        }
        return FallbackDecrypt(encryptedData, password);
    }

    public async Task<string> EncryptNostrContentAsync(string nsec, string npub, string content)
    {
        if (_isJsRuntimeAvailable)
        {
            return await _encryptionService.EncryptNostrContentAsync(nsec, npub, content);
        }
        return FallbackEncryptNostr(nsec, npub, content);
    }

    public async Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
    {
        if (_isJsRuntimeAvailable)
        {
            return await _encryptionService.DecryptNostrContentAsync(nsec, npub, encryptedContent);
        }
        return FallbackDecryptNostr(nsec, npub, encryptedContent);
    }

    private string FallbackEncrypt(string data, string password)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data + password));
    }

    private string FallbackDecrypt(string data, string password)
    {
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(data)).Replace(password, "");
    }

    private string FallbackEncryptNostr(string nsec, string npub, string content)
    {
        // Add a simplified fallback logic for encryption
        return $"EncryptedContent:{content}";
    }

    private string FallbackDecryptNostr(string nsec, string npub, string encryptedContent)
    {
        // Add a simplified fallback logic for decryption
        return $"DecryptedContent:{encryptedContent}";
    }
}


public class WalletStorage : IWalletStorage
{
    private readonly Dictionary<string, object> _storage = new();

    private const string WalletKey = "wallet";

    public bool HasWallet()
    {
        return _storage.ContainsKey(WalletKey);
    }

    public void SaveWalletWords(Wallet wallet)
    {
        if (_storage.ContainsKey(WalletKey))
        {
            throw new ArgumentNullException("Wallet already exists!");
        }

        _storage[WalletKey] = wallet;
    }

    public void DeleteWallet()
    {
        if (_storage.ContainsKey(WalletKey))
        {
            _storage.Remove(WalletKey);
        }
    }

    public Wallet GetWallet()
    {
        if (_storage.TryGetValue(WalletKey, out var wallet) && wallet is Wallet castedWallet)
        {
            return castedWallet;
        }

        throw new ArgumentNullException("Wallet not found!");
    }

    public void SetFounderKeys(FounderKeyCollection founderPubKeys)
    {
        var wallet = GetWallet();
        wallet.FounderKeys = founderPubKeys;

        _storage[WalletKey] = wallet;
    }

    public FounderKeyCollection GetFounderKeys()
    {
        return GetWallet().FounderKeys;
    }
}




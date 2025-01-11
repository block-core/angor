using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using Nostr.Client.Client;
using Blockcore.Networks; 	
using Angor.Shared.Networks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Angor.Client;





class Program
{
    private const string RelayUrl = "wss://relay.angor.io/"; // Replace with your relay
    private const string RecipientPrivateKey = "362f4b51ecac1bc07a3216d9b5da1abfcb42f04f51ebfd659d5ebfc21f62b05d"; // Your private key
    private const string RecipientPublicKey = "b61f43c4a88d538ee1e74979b75c4d54fd5ff756923f14aef0366bdab9b3cbcc"; // Corresponding public key
private static ISignService _signService;
    private readonly INetworkConfiguration _networkConfiguration;


public Program(ISignService signService, INetworkConfiguration networkConfiguration)
{
    _signService = signService ?? throw new ArgumentNullException(nameof(signService));
    _networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
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
            services.AddSingleton<INetworkConfiguration, NetworkConfiguration>();
            services.AddLogging();
            services.AddSingleton<Program>();
        });


    public async Task RunAsync()
    {
        Console.WriteLine("Initializing application...");

        using var client = new ClientWebSocket();

        try
        {
            Console.WriteLine("Connecting to relay...");
            await client.ConnectAsync(new Uri(RelayUrl), CancellationToken.None);
            Console.WriteLine("Connected to relay.");

            Console.WriteLine("Subscribing to encrypted DM...");
            await SubscribeToEncryptedDM(client, RecipientPublicKey);

            Console.WriteLine("Listening for messages...");
            await ListenForMessages(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }
        finally
        {
            if (client.State == WebSocketState.Open || client.State == WebSocketState.Connecting)
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", CancellationToken.None);
            }
            Console.WriteLine("WebSocket client closed.");
        }

        Console.WriteLine("Application exiting...");
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
HandleEventMessage(response, client, _signService);
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



    private static void HandleEventMessage(object[] response, ClientWebSocket client, ISignService signService)
{
    try
    {
        if (response.Length > 2 && response[2] is JsonElement eventData)
        {
            Console.WriteLine($"Raw event data: {eventData}");

            var nostrEvent = JsonSerializer.Deserialize<NostrEvent>(eventData.GetRawText());
            if (nostrEvent == null)
            {
                Console.WriteLine("Deserialized NostrEvent is null.");
                return;
            }

            Console.WriteLine($"NostrEvent: {nostrEvent}");
            Console.WriteLine($"NostrEvent Kind: {nostrEvent.Kind}");

            if (nostrEvent.Kind == 4 && !string.IsNullOrEmpty(nostrEvent.Content))
            {
                string decryptedContent = DecryptMessage(RecipientPrivateKey, nostrEvent.Content, nostrEvent.Pubkey);
                if (!string.IsNullOrEmpty(decryptedContent))
                {
                    Console.WriteLine($"Decrypted message: {decryptedContent}");

                    string signedTransactionHex = BitcoinUtils.DecodeAndSignTransaction(decryptedContent, RecipientPrivateKey);
                    if (!string.IsNullOrEmpty(signedTransactionHex))
                    {
                        Console.WriteLine($"Signed Transaction Hex: {signedTransactionHex}");
                        SendSignaturesToInvestor(
    signedTransactionHex,
    RecipientPrivateKey,
    nostrEvent.Pubkey,
    nostrEvent.Id,
    client,
    _signService // Pass the instance
);
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


private static string DecryptMessage(string recipientPrivateKey, string encryptedContent, string senderPublicKey)
{
    try
    {
        var parts = encryptedContent.Split("?iv=");
        if (parts.Length != 2)
        {
            Console.WriteLine("Invalid encrypted content format.");
            return null;
        }

        var cipherText = Convert.FromBase64String(parts[0]);
        var iv = Convert.FromBase64String(parts[1]);

        var privateKey = new Key(Encoders.Hex.DecodeData(recipientPrivateKey));
        var publicKey = new PubKey("02" + senderPublicKey);

        var sharedSecret = publicKey.GetSharedPubkey(privateKey).ToBytes()[1..]; // Drop the prefix byte

        using var aes = Aes.Create();
        aes.Key = sharedSecret;
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


    private static void SendSignaturesToInvestor(
    string signedTransactionHex,
    string recipientPrivateKey,
    string investorPubKey,
    string eventId,
    ClientWebSocket client,
    ISignService signService)
{
    if (signService == null)
    {
        Console.WriteLine("[ERROR] SignService is not initialized. Unable to send signatures.");
        return;
    }

    try
    {
        var creationTime = signService.SendSignaturesToInvestor(
            signedTransactionHex,
            recipientPrivateKey,
            investorPubKey,
            eventId
        );

        Console.WriteLine($"[INFO] Signed event sent successfully at {creationTime}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to send signed event: {ex.Message}");
    }
}





}

// Classes and Utility Functions:
public class NostrEvent
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("kind")] public int Kind { get; set; }
    [JsonPropertyName("pubkey")] public string Pubkey { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; }
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


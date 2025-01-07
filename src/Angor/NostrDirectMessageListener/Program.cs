using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using System.Security.Cryptography;


class Program
{
    private const string RelayUrl = "wss://relay.angor.io/"; // Replace with your relay
    private const string RecipientPrivateKey = "15839d7dc2355aad183c4c4ad6efdced46550146be2a2a5a0b35141bb75123cc"; // Your private key
    private const string RecipientPublicKey = "3f5148bf155a224bd82b66b18e2000b28a73422ef05e0e348ddde22b68b1138d"; // Corresponding public key

    static async Task Main(string[] args)
    {
        Console.WriteLine($"Connecting to relay: {RelayUrl}");

        using var client = new ClientWebSocket();

        try
        {
            await client.ConnectAsync(new Uri(RelayUrl), CancellationToken.None);
            Console.WriteLine("Connected to relay.");

            // Send a subscription to listen for Encrypted DM messages
            await SubscribeToEncryptedDM(client, RecipientPublicKey);

            // Listen for incoming messages
            await ListenForMessages(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task SubscribeToEncryptedDM(ClientWebSocket client, string publicKey)
    {
        // Construct the subscription message manually
        string subscriptionId = Guid.NewGuid().ToString(); // Unique subscription ID
        string subscriptionMessage = $@"
    [
        ""REQ"",
        ""{subscriptionId}"",
        {{
            ""kinds"": [4],
            ""#p"": [""{publicKey}""]
        }}
    ]";

        Console.WriteLine($"Subscribing to Encrypted DMs for public key: {publicKey}");
        Console.WriteLine($"Subscription JSON: {subscriptionMessage}");

        // Send the JSON message to the relay
        await client.SendAsync(
            Encoding.UTF8.GetBytes(subscriptionMessage.Trim()),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }




    private static async Task ListenForMessages(ClientWebSocket client)
    {
        var buffer = new byte[1024 * 64]; // 64 KB buffer

        while (client.State == WebSocketState.Open)
        {
            try
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Message received: {message}");

                    // Process the message
                    ProcessNostrEvent(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket connection closed.");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                break;
            }
        }
    }

    private static void ProcessNostrEvent(string message)
    {
        try
        {
            if (message.StartsWith("["))
            {
                var response = JsonSerializer.Deserialize<object[]>(message);
                if (response == null || response.Length == 0)
                {
                    Console.WriteLine("Received empty or malformed message.");
                    return;
                }

                string eventType = response[0]?.ToString();
                Console.WriteLine($"Message type: {eventType}");

                switch (eventType)
                {
                    case "EVENT":
                        HandleEventMessage(response);
                        break;

                    case "NOTICE":
                        Console.WriteLine($"Relay notice: {response[1]}");
                        break;

                    case "EOSE":
                        Console.WriteLine("End of stored events (EOSE) received.");
                        break;

                    default:
                        Console.WriteLine($"Unknown message type: {message}");
                        break;
                }
            }
            else
            {
                Console.WriteLine($"Non-array message received: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}\nRaw Message: {message}");
        }
    }

    
    private static void HandleEventMessage(object[] response)
    {
        try
        {
            if (response.Length > 2 && response[2] is JsonElement eventData)
            {
                var nostrEvent = JsonSerializer.Deserialize<NostrEvent>(eventData.GetRawText());
                if (nostrEvent != null && nostrEvent.Kind == 4)
                {
                    Console.WriteLine($"Encrypted DM received. Content: {nostrEvent.Content}");

                    // Decrypt the message
                    string decryptedContent = DecryptMessage(RecipientPrivateKey, nostrEvent.Content, nostrEvent.Pubkey);
                    if (!string.IsNullOrEmpty(decryptedContent))
                    {
                        Console.WriteLine($"Decrypted message: {decryptedContent}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to decrypt the message.");
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
            // Split the content into ciphertext and IV
            var parts = encryptedContent.Split("?iv=");
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
        var privateKey = new Blockcore.NBitcoin.Key(Blockcore.NBitcoin.DataEncoders.Encoders.Hex.DecodeData(recipientPrivateKeyHex));
        var publicKey = new Blockcore.NBitcoin.PubKey("02" + senderPublicKeyHex); // Add '02' for compressed key format


        // Compute the shared secret
        var sharedSecret = publicKey.GetSharedPubkey(privateKey);

        // Return the shared secret in hex without prefix
        return Blockcore.NBitcoin.DataEncoders.Encoders.Hex.EncodeData(sharedSecret.ToBytes()[1..]);
    }


}

public class NostrEvent
{
    public string Id { get; set; }
    public int Kind { get; set; }
    public string Pubkey { get; set; }
    public string Content { get; set; }
    public long CreatedAt { get; set; }
    public string Sig { get; set; }
    public List<List<string>> Tags { get; set; }
}

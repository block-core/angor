extern alias NBitcoinAlias;

using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using NBitcoinAlias::NBitcoin; // Use the alias here for NBitcoin


class Program
{
    private const string RelayUrl = "wss://relay.angor.io/"; // Replace with your relay
    private const string RecipientPrivateKey = "362f4b51ecac1bc07a3216d9b5da1abfcb42f04f51ebfd659d5ebfc21f62b05d"; // Your private key
    private const string RecipientPublicKey = "b61f43c4a88d538ee1e74979b75c4d54fd5ff756923f14aef0366bdab9b3cbcc"; // Corresponding public key

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
        string subscriptionId = Guid.NewGuid().ToString();
        string subscriptionMessage = $@"
    [
        ""REQ"",
        ""{subscriptionId}"",
        {{
            ""kinds"": [4],
            ""#p"": [""{publicKey}""]
        }}
    ]";

        Console.WriteLine($"Subscribing with JSON: {subscriptionMessage}");
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
            Console.WriteLine($"Processing message: {message}");

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
                        Console.WriteLine("Handling EVENT type...");
                        HandleEventMessage(response);
                        break;

                    case "NOTICE":
                        Console.WriteLine($"Relay notice: {response[1]}");
                        break;

                    case "EOSE":
                        Console.WriteLine("End of stored events (EOSE) received.");
                        break;

                    default:
                        Console.WriteLine($"Unknown message type: {eventType}");
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
                Console.WriteLine($"Raw event data: {eventData}");

                try
                {
                    var nostrEvent = JsonSerializer.Deserialize<NostrEvent>(eventData.GetRawText());
                    Console.WriteLine($"NostrEvent: {nostrEvent}");
                    Console.WriteLine($"NostrEvent Kind: {nostrEvent?.Kind}");
                    Console.WriteLine($"Raw JSON: {eventData.GetRawText()}");

                    if (nostrEvent != null && nostrEvent.Kind == 4)
                    {
                        Console.WriteLine($"Encrypted DM received. Content: {nostrEvent.Content}");

                        string decryptedContent = DecryptMessage(RecipientPrivateKey, nostrEvent.Content, nostrEvent.Pubkey);
                        if (!string.IsNullOrEmpty(decryptedContent))
                        {
                            Console.WriteLine($"Decrypted message: {decryptedContent}");

                            // Decode and sign the Bitcoin transaction
                            DecodeAndSignBitcoinTransaction(decryptedContent);
                        }
                        else
                        {
                            Console.WriteLine("Failed to decrypt the message.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"NostrEvent deserialized but not of kind 4. Kind: {nostrEvent?.Kind}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializing NostrEvent: {ex.Message}\nRaw JSON: {eventData.GetRawText()}");
                }
            }
            else
            {
                Console.WriteLine("Event data missing or not a JsonElement.");
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



    private static void DecodeBitcoinTransaction(string rawTransactionHex)
    {
        try
        {
            var network = NBitcoinAlias::NBitcoin.Network.TestNet; // Explicitly use NBitcoin
            var transaction = NBitcoinAlias::NBitcoin.Transaction.Parse(rawTransactionHex, network);

            Console.WriteLine("Decoded Bitcoin Transaction:");
            Console.WriteLine($" - Transaction ID: {transaction.GetHash()}");
            Console.WriteLine($" - Version: {transaction.Version}");
            Console.WriteLine($" - LockTime: {transaction.LockTime}");

            Console.WriteLine("Inputs:");
            foreach (var input in transaction.Inputs)
            {
                Console.WriteLine($"   - Previous Tx Hash: {input.PrevOut.Hash}");
                Console.WriteLine($"   - Index: {input.PrevOut.N}");
                Console.WriteLine($"   - ScriptSig: {input.ScriptSig}");
            }

            Console.WriteLine("Outputs:");
            foreach (var output in transaction.Outputs)
            {
                Console.WriteLine($"   - Value: {output.Value} (satoshis)");
                Console.WriteLine($"   - ScriptPubKey: {output.ScriptPubKey}");
                Console.WriteLine($"   - Address: {output.ScriptPubKey.GetDestinationAddress(network)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decoding Bitcoin transaction: {ex.Message}");
        }
    }

        
    private static void DecodeAndSignBitcoinTransaction(string rawTransactionHex)
{
    try
    {
        // Parse the transaction
        var network = NBitcoinAlias::NBitcoin.Network.Main; // Explicitly use NBitcoin
        var transaction = NBitcoinAlias::NBitcoin.Transaction.Parse(rawTransactionHex, network);

        Console.WriteLine("Decoded Bitcoin Transaction:");
        Console.WriteLine($" - Transaction ID: {transaction.GetHash()}");

        // Example private key
        string privateKeyHex = "362f4b51ecac1bc07a3216d9b5da1abfcb42f04f51ebfd659d5ebfc21f62b05d";

        // Example coins (UTXOs)
        var coins = new List<NBitcoinAlias::NBitcoin.ICoin>
        {
            new NBitcoinAlias::NBitcoin.Coin(
                new NBitcoinAlias::NBitcoin.OutPoint(NBitcoinAlias::NBitcoin.uint256.Parse("previousTransactionHash"), 0), // Use the correct alias
                new NBitcoinAlias::NBitcoin.TxOut(
                    NBitcoinAlias::NBitcoin.Money.Coins(0.01m), // Amount in BTC
                    new NBitcoinAlias::NBitcoin.Script("scriptPubKey") // Replace with actual ScriptPubKey
                )
            )
        };

        // Sign the transaction
        string signedTransactionHex = SignTransaction(transaction, privateKeyHex, coins, network);
        Console.WriteLine($"Signed Transaction Hex: {signedTransactionHex}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error decoding or signing Bitcoin transaction: {ex.Message}");
    }
}

private static string SignTransaction(
    NBitcoinAlias::NBitcoin.Transaction transaction,
    string privateKeyHex,
    List<NBitcoinAlias::NBitcoin.ICoin> coins,
    NBitcoinAlias::NBitcoin.Network network)
{
    try
    {
        // Decode the private key
        var privateKey = new NBitcoinAlias::NBitcoin.Key(Encoders.Hex.DecodeData(privateKeyHex));

        // Initialize the TransactionBuilder
        var builder = network.CreateTransactionBuilder();

        // Add coins and private key
        builder.AddCoins(coins);
        builder.AddKeys(privateKey);

        // Sign the transaction
        var signedTransaction = builder.SignTransaction(transaction);

        Console.WriteLine("Transaction successfully signed.");
        return signedTransaction.ToHex();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error signing transaction: {ex.Message}");
        return null;
    }
}





}

public class NostrEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("kind")]
    public int Kind { get; set; } // Ensure it is an integer and matches the JSON field

    [JsonPropertyName("pubkey")]
    public string Pubkey { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("sig")]
    public string Sig { get; set; }

    [JsonPropertyName("tags")]
    public List<List<string>> Tags { get; set; }
}

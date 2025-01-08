extern alias NBitcoinAlias; // Alias for NBitcoin

using System;
using System.Numerics;
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
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
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
                    ProcessNostrEvent(message,client);
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

    private static void ProcessNostrEvent(string message,ClientWebSocket client )
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
                        HandleEventMessage(response, client);
                        break;

                    case "NOTICE":
                        Console.WriteLine($"Relay notice: {response[1]}");
                        if (response.Length > 1)
                        {
                            string noticeMessage = response[1]?.ToString();
                            Console.WriteLine($"Relay Notice Details: {noticeMessage}");
                        }
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

    private static void HandleEventMessage(object[] response, ClientWebSocket client)
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

                        string signedTransactionHex = DecodeAndSignBitcoinTransaction(decryptedContent, client);
                        if (signedTransactionHex != null)
                        {
                            Console.WriteLine($"Signed Transaction Hex: {signedTransactionHex}");
                            SendSignaturesToInvestor(
                                signedTransactionHex,
                                RecipientPrivateKey,
                                nostrEvent.Pubkey,
                                nostrEvent.Id,
                                client
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


    
    private static void DecodeAndSignBitcoinTransaction(string rawTransactionHex)
    {
        try
        {
            var network = NBitcoinAlias::NBitcoin.Network.Main;
            var transaction = NBitcoinAlias::NBitcoin.Transaction.Parse(rawTransactionHex, network);

            Console.WriteLine("Decoded Bitcoin Transaction:");
            Console.WriteLine($" - Transaction ID: {transaction.GetHash()}");

            string privateKeyHex = "362f4b51ecac1bc07a3216d9b5da1abfcb42f04f51ebfd659d5ebfc21f62b05d";

            var coins = new List<NBitcoinAlias::NBitcoin.ICoin>
            {
                new NBitcoinAlias::NBitcoin.Coin(
                    new NBitcoinAlias::NBitcoin.OutPoint(
                        NBitcoinAlias::NBitcoin.uint256.Parse("2f492d7850fc289039b4be57f93b704e88add6a1e48f8d860a121286a1aa0611"),
                        0
                    ),
                    new NBitcoinAlias::NBitcoin.TxOut(
                        NBitcoinAlias::NBitcoin.Money.Coins(0.01m),
                        NBitcoinAlias::NBitcoin.Script.FromHex("76a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac")
                    )
                )
            };

            string signedTransactionHex = SignTransaction(transaction, privateKeyHex, coins, network);
            Console.WriteLine($"Signed Transaction Hex: {signedTransactionHex}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decoding or signing Bitcoin transaction: {ex.Message}");
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

        
    private static string DecodeAndSignBitcoinTransaction(string rawTransactionHex, ClientWebSocket client)
    {
        try
        {
            var network = NBitcoinAlias::NBitcoin.Network.Main;
            var transaction = NBitcoinAlias::NBitcoin.Transaction.Parse(rawTransactionHex, network);

            string privateKeyHex = "362f4b51ecac1bc07a3216d9b5da1abfcb42f04f51ebfd659d5ebfc21f62b05d";

            // Example coins (UTXOs)
            var coins = new List<NBitcoinAlias::NBitcoin.ICoin>
            {
                new NBitcoinAlias::NBitcoin.Coin(
                    new NBitcoinAlias::NBitcoin.OutPoint(
                        NBitcoinAlias::NBitcoin.uint256.Parse("2f492d7850fc289039b4be57f93b704e88add6a1e48f8d860a121286a1aa0611"), // Use NBitcoin's uint256
                        0 // Index of the UTXO in the transaction outputs
                    ),
                    new NBitcoinAlias::NBitcoin.TxOut(
                        NBitcoinAlias::NBitcoin.Money.Coins(0.01m), // Amount in BTC
                        NBitcoinAlias::NBitcoin.Script.FromHex("76a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac") // Replace with actual ScriptPubKey
                    )
                )
            };

            return SignTransaction(transaction, privateKeyHex, coins, network);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decoding or signing Bitcoin transaction: {ex.Message}");
            return null;
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


    private static void SendSignaturesToInvestor(
        string encryptedSignatureInfo,
        string nostrPrivateKeyHex,
        string investorNostrPubKey,
        string eventId,
        ClientWebSocket client)
    {
        if (client == null)
        {
            Console.WriteLine("Error: WebSocket client is null.");
            return;
        }

        try
        {
            var nostrPrivateKey = new Blockcore.NBitcoin.Key(Encoders.Hex.DecodeData(nostrPrivateKeyHex));

            var ev = new NostrEvent
            {
                Kind = 4, // Encrypted Direct Message
                CreatedAt = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                Content = encryptedSignatureInfo,
                Tags = new List<List<string>>
                {
                    new List<string> { "p", investorNostrPubKey },
                    new List<string> { "e", eventId },
                    new List<string> { "subject", "Re:Investment offer" }
                }
            };

            // Sign the event
            var signedEvent = SignEvent(ev, nostrPrivateKey);

            // Serialize the signed event
            var signedEventJson = JsonSerializer.Serialize(signedEvent, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Outgoing Signed Event JSON: {signedEventJson}");
            // Send the event to the relay
            Console.WriteLine($"Sending signed event to relay...");
            client.SendAsync(
                Encoding.UTF8.GetBytes(signedEventJson),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            ).Wait();

            Console.WriteLine("Signed event successfully sent to relay.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending signatures to investor: {ex.Message}");
        }
    }


    private static NostrEvent SignEvent(NostrEvent nostrEvent, Blockcore.NBitcoin.Key privateKey)
    {
        try
        {
            // Prepare the event content
            var eventContent = $"{nostrEvent.Kind}:{nostrEvent.CreatedAt}:{nostrEvent.Content}";
            var eventBytes = Encoding.UTF8.GetBytes(eventContent);

            // Hash the content
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(eventBytes);

            // Convert hash to uint256
            var hash = new Blockcore.NBitcoin.uint256(hashedBytes);

            // Sign the hash
            var ecdsaSignature = privateKey.Sign(hash);

            // Ensure signature is in 64-byte raw format (R || S)
            var signatureRaw = ecdsaSignature.To64ByteArray();

            // Convert to hex string
            var signatureHex = Blockcore.NBitcoin.DataEncoders.Encoders.Hex.EncodeData(signatureRaw);

            // Validate the signature length
            if (signatureHex.Length != 128)
            {
                throw new InvalidOperationException($"Invalid signature length: {signatureHex.Length}. Signature: {signatureHex}");
            }

            // Assign the signature to the Nostr event
            nostrEvent.Sig = signatureHex;

            Console.WriteLine("Event successfully signed.");
            return nostrEvent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error signing Nostr event: {ex.Message}");
            throw;
        }
    }
    
    

}

public class NostrEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public int Kind { get; set; }

    [JsonPropertyName("pubkey")]
    public string Pubkey { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } // Change to long for Unix timestamp

    [JsonPropertyName("sig")]
    public string Sig { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<List<string>> Tags { get; set; } = new();
}

// Create a static class for extensions
public static class ECDSASignatureExtensions
{
    public static byte[] To64ByteArray(this Blockcore.NBitcoin.Crypto.ECDSASignature signature)
    {
        var r = ToBytesUnsigned(signature.R);
        var s = ToBytesUnsigned(signature.S);

        // Ensure R and S are exactly 32 bytes each
        var rPadded = new byte[32];
        var sPadded = new byte[32];
        Array.Copy(r, 0, rPadded, 32 - r.Length, r.Length);
        Array.Copy(s, 0, sPadded, 32 - s.Length, s.Length);

        // Concatenate R and S
        var rawSignature = new byte[64];
        Array.Copy(rPadded, 0, rawSignature, 0, 32);
        Array.Copy(sPadded, 0, rawSignature, 32, 32);

        return rawSignature;
    }

    private static byte[] ToBytesUnsigned(Blockcore.NBitcoin.BouncyCastle.math.BigInteger value)
    {
        return value.ToByteArrayUnsigned();
    }

    
    private static System.Numerics.BigInteger ConvertToSystemBigInteger(Blockcore.NBitcoin.BouncyCastle.math.BigInteger value)
    {
        var bytes = value.ToByteArrayUnsigned();
        return new System.Numerics.BigInteger(bytes.Reverse().ToArray()); // Ensure proper byte order
    }



    private static byte[] ToBytesUnsigned(BigInteger value)
    {
        var bytes = value.ToByteArray();

        // Ensure the bytes are unsigned
        if (bytes[^1] == 0x00) // Remove leading zero byte if present
        {
            Array.Resize(ref bytes, bytes.Length - 1);
        }

        Array.Reverse(bytes); // Reverse for Big-Endian order
        return bytes;
    }
}



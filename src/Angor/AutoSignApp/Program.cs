// Standard namespaces
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

// Aliases for resolving conflicts
using NBitcoinCore = global::NBitcoin; // Alias for NBitcoin
using BlockcoreNBitcoin = global::Blockcore.NBitcoin; // Alias for Blockcore.NBitcoin

// Specific namespaces from your project
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;




class Program
{
    static async Task Main(string[] args)
    {
        string relayUrl = "wss://relay.angor.io/";
        Console.WriteLine($"Connecting to relay: {relayUrl}");

        using var client = new ClientWebSocket();

        try
        {
            await client.ConnectAsync(new Uri(relayUrl), CancellationToken.None);
            Console.WriteLine("Connected to relay.");

            await ListenForMessages(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

	static async Task ListenForMessages(ClientWebSocket client)
{
    var buffer = new byte[1024 * 4];

    while (client.State == WebSocketState.Open)
    {
        Console.WriteLine("Waiting for a message...");

        try
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Console.WriteLine($"WebSocket State: {client.State}");
            Console.WriteLine($"Result Count: {result.Count}");

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Message received: {message}");
                HandleRelayMessage(message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("WebSocket closed by server.");
                break;
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            break;
        }
    }

    Console.WriteLine("Exiting ListenForMessages loop.");
}




    private static void HandleRelayMessage(string message)
{
    try
    {
        Console.WriteLine("Handling incoming message...");
        var signatureRequest = System.Text.Json.JsonSerializer.Deserialize<SignatureRequest>(message);

        if (signatureRequest != null)
        {
            Console.WriteLine($"Parsed SignatureRequest: {signatureRequest}");
            if (!string.IsNullOrEmpty(signatureRequest.EncryptedMessage))
            {
                Console.WriteLine($"Processing signature request from {signatureRequest.InvestorNostrPubKey}...");
                AutoSignRequest(signatureRequest);
            }
            else
            {
                Console.WriteLine("Invalid SignatureRequest: EncryptedMessage is null or empty.");
            }
        }
        else
        {
            Console.WriteLine("Failed to parse SignatureRequest.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error handling message: {ex.Message}\nMessage Content: {message}");
    }
}


    private static void AutoSignRequest(SignatureRequest request)
    {
        try
        {
            string privateKey = "15839d7dc2355aad183c4c4ad6efdced46550146be2a2a5a0b35141bb75123cc"; // Replace with actual private key
            string decryptedTransactionHex = DecryptNostrContent(privateKey, request.InvestorNostrPubKey, request.EncryptedMessage);

            if (!string.IsNullOrEmpty(decryptedTransactionHex))
            {
                Console.WriteLine($"Decrypted Transaction Hex: {decryptedTransactionHex}");

                string signedTransaction = SignTransaction(decryptedTransactionHex, privateKey);

                if (!string.IsNullOrEmpty(signedTransaction))
                {
                    Console.WriteLine($"Signed Transaction: {signedTransaction}");
                    SendSignedTransaction(request, signedTransaction);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error signing request: {ex.Message}");
        }
    }

    private static string DecryptNostrContent(string nsec, string npub, string encryptedContent)
{
    try
    {
        Console.WriteLine("Decrypting Nostr content...");
        Console.WriteLine($"nsec: {nsec}, npub: {npub}, encryptedContent: {encryptedContent}");

        string sharedSecretHex = GetSharedSecretHexWithoutPrefix(nsec, npub);
        string decryptedContent = DecryptWithSharedSecret(encryptedContent, sharedSecretHex);

        Console.WriteLine($"Decrypted Content: {decryptedContent}");
        return decryptedContent;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error decrypting Nostr content: {ex.Message}");
        return null;
    }
}


    private static string GetSharedSecretHexWithoutPrefix(string nsec, string npub)
	{
    	var privateKey = new Blockcore.NBitcoin.Key(Blockcore.NBitcoin.DataEncoders.Encoders.Hex.DecodeData(nsec));
   		var publicKey = new Blockcore.NBitcoin.PubKey("02" + npub);

 		var sharedSecret = publicKey.GetSharedPubkey(privateKey);
    	return Blockcore.NBitcoin.DataEncoders.Encoders.Hex.EncodeData(sharedSecret.ToBytes()[1..]);
	}


    private static string DecryptWithSharedSecret(string encryptedContent, string sharedSecretHex)
    {
        var key = Convert.FromHexString(sharedSecretHex);
        var combined = Convert.FromBase64String(encryptedContent);

        using var aes = Aes.Create();
        aes.Key = key;

        var iv = new byte[16];
        var ciphertext = new byte[combined.Length - 16];
        Array.Copy(combined, 0, iv, 0, 16);
        Array.Copy(combined, 16, ciphertext, 0, ciphertext.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var decryptedBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private static string SignTransaction(string transactionHex, string privateKeyHex)
{
    try
    {
        Console.WriteLine("Signing transaction...");
        Console.WriteLine($"Transaction Hex: {transactionHex}");
        Console.WriteLine($"Private Key Hex: {privateKeyHex}");

        var key = new NBitcoinCore.Key(NBitcoinCore.DataEncoders.Encoders.Hex.DecodeData(privateKeyHex));
        var bitcoinSecret = new NBitcoinCore.BitcoinSecret(key, NBitcoinCore.Network.TestNet);
        var transaction = NBitcoinCore.Transaction.Parse(transactionHex, NBitcoinCore.Network.TestNet);

        Console.WriteLine("Signing transaction with BitcoinSecret...");
        transaction.Sign(bitcoinSecret, new NBitcoinCore.ICoin[0]); // Adjust coin array if needed

        string signedTransactionHex = transaction.ToHex();
        Console.WriteLine($"Signed Transaction Hex: {signedTransactionHex}");
        return signedTransactionHex;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error signing transaction: {ex.Message}");
        return null;
    }
}






    private static async Task SendSignedTransaction(SignatureRequest request, string signedTransaction)
{
    try
    {
        Console.WriteLine("Sending signed transaction...");
        string relayUrl = "wss://relay.angor.io/";

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri(relayUrl), CancellationToken.None);
        Console.WriteLine("Connected to relay for sending transaction.");

        var response = new
        {
            @event = "signed",
            signedTransaction,
            request.EventId
        };

        string jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);
        Console.WriteLine($"JSON Response: {jsonResponse}");

        var buffer = Encoding.UTF8.GetBytes(jsonResponse);
        await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        Console.WriteLine($"Signed transaction sent for EventId: {request.EventId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending signed transaction: {ex.Message}");
    }
}


}

public class SignatureRequest
{
    public string? InvestorNostrPubKey { get; set; }
    public string? EncryptedMessage { get; set; }
    public string? EventId { get; set; }
}

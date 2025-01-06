using System;
using System.Security.Cryptography;
using System.Text;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using System.Net.WebSockets;

class Program
{
    static async Task Main(string[] args)
    {
        string relayUrl = "wss://relay.angor.io"; // Replace with actual relay URL
        Console.WriteLine($"Connecting to relay: {relayUrl}");

        using var ws = new WebSocket(relayUrl);

        ws.OnMessage += (sender, e) =>
        {
            Console.WriteLine($"Message received: {e.Data}");
            HandleRelayMessage(e.Data);
        };

        ws.OnOpen += (sender, e) =>
        {
            Console.WriteLine("Connected to relay.");
        };

        ws.OnClose += (sender, e) =>
        {
            Console.WriteLine("Disconnected from relay.");
        };

        ws.Connect();
        Console.WriteLine("Listening for messages...");
        Console.ReadLine(); // Keep the connection alive
    }

    private static void HandleRelayMessage(string message)
    {
        try
        {
            var signatureRequest = System.Text.Json.JsonSerializer.Deserialize<SignatureRequest>(message);

            if (signatureRequest != null && !string.IsNullOrEmpty(signatureRequest.EncryptedMessage))
            {
                Console.WriteLine($"Processing signature request from {signatureRequest.InvestorNostrPubKey}...");
                AutoSignRequest(signatureRequest);
            }
            else
            {
                Console.WriteLine("Invalid message or no encrypted content found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
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
        string sharedSecretHex = GetSharedSecretHexWithoutPrefix(nsec, npub);
        return DecryptWithSharedSecret(encryptedContent, sharedSecretHex);
    }

    private static string GetSharedSecretHexWithoutPrefix(string nsec, string npub)
    {
        var privateKey = new Key(Encoders.Hex.DecodeData(nsec));
        var publicKey = new PubKey("02" + npub);

        var sharedSecret = publicKey.GetSharedPubkey(privateKey);
        return Encoders.Hex.EncodeData(sharedSecret.ToBytes()[1..]);
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
            var key = new Key(Encoders.Hex.DecodeData(privateKeyHex));
            var transaction = Transaction.Parse(transactionHex, Network.Main);

            transaction.Sign(key, true);
            return transaction.ToHex();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error signing transaction: {ex.Message}");
            return null;
        }
    }

    private static void SendSignedTransaction(SignatureRequest request, string signedTransaction)
    {
        string relayUrl = "wss://relay.angor.io"; // Replace with actual relay URL
        using var ws = new WebSocket(relayUrl);

        var response = new
        {
            @event = "signed",
            signedTransaction,
            request.EventId
        };

        string jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);

        ws.Connect();
        ws.Send(jsonResponse);
        Console.WriteLine($"Signed transaction sent for EventId: {request.EventId}");
    }
}

public class SignatureRequest
{
    public string InvestorNostrPubKey { get; set; }
    public string EncryptedMessage { get; set; }
    public string EventId { get; set; }
}

using WebSocketSharp;
using Newtonsoft.Json;
using Blockcore.NBitcoin;

class Program
{
    static void Main(string[] args)
    {
        string relayUrl = "wss://relay.angor.io"; // Relay URL
        using (var ws = new WebSocket(relayUrl))
        {
            ws.OnMessage += (sender, e) =>
            {
                Console.WriteLine($"Message received: {e.Data}");
                HandleRelayMessage(e.Data);
            };

            ws.Connect();
            Console.WriteLine("Connected to relay.");
            Console.ReadLine(); // Keep the connection alive
        }
    }

    static void HandleRelayMessage(string message)
    {
        try
        {
            var signatureRequest = JsonConvert.DeserializeObject<SignatureRequest>(message);

            if (signatureRequest != null && !string.IsNullOrEmpty(signatureRequest.TransactionHex))
            {
                Console.WriteLine($"Processing signature request from {signatureRequest.InvestorNostrPubKey}...");
                AutoSignRequest(signatureRequest);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
        }
    }

    static void AutoSignRequest(SignatureRequest request)
    {
        try
        {
            // Load the founder's private key securely
            string founderPrivateKeyHex = "your-private-key"; // Replace with a securely loaded private key

            // Decrypt the request if necessary
            string transactionHex = DecryptTransaction(request.EncryptedMessage, founderPrivateKeyHex);

            // Sign the transaction
            string signedTransaction = SignTransaction(transactionHex, founderPrivateKeyHex);

            // Send the signed transaction back to the relay
            SendSignedTransaction(request, signedTransaction);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error signing request: {ex.Message}");
        }
    }

    static string DecryptTransaction(string encryptedMessage, string privateKeyHex)
    {
        try
        {
            // Implement decryption logic using your cryptography service
            string decryptedMessage = encryptedMessage; // Replace with actual decryption logic
            return decryptedMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decrypting transaction: {ex.Message}");
            return null;
        }
    }

    static string SignTransaction(string transactionHex, string privateKeyHex)
    {
        try
        {
            // Use NBitcoin to sign the transaction
            var key = new Key(Encoders.Hex.DecodeData(privateKeyHex));
            var transaction = Transaction.Parse(transactionHex, Network.Main);

            // Sign and return the transaction
            transaction.Sign(key, true);
            return transaction.ToHex();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error signing transaction: {ex.Message}");
            return null;
        }
    }

    static void SendSignedTransaction(SignatureRequest request, string signedTransaction)
    {
        try
        {
            string relayUrl = "wss://relay.angor.io"; // Relay URL
            using (var ws = new WebSocket(relayUrl))
            {
                var response = new
                {
                    event = "signed",
                    signedTransaction,
                    request.EventId
                };

                string jsonResponse = JsonConvert.SerializeObject(response);
                ws.Connect();
                ws.Send(jsonResponse);
                Console.WriteLine($"Signed transaction sent for EventId: {request.EventId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending signed transaction: {ex.Message}");
        }
    }
}

// Define the SignatureRequest class
public class SignatureRequest
{
    public string InvestorNostrPubKey { get; set; }
    public string EncryptedMessage { get; set; }
    public string TransactionHex { get; set; }
    public string EventId { get; set; }
}

using System.Security.Cryptography;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.JSInterop;

namespace Angor.Client.Services;

public class NostrTools : INostrEncryptionService
{
    private readonly IJSRuntime _jsRuntime;

    public NostrTools(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> EncryptNostrContentAsync(string nsec, string npub, string content)
    {
        var secertHex = GetAesConversationKey(nsec, npub);
            
        return await _jsRuntime.InvokeAsync<string>("encryptNostr", secertHex, content);
    }

    public async Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
    {
        var secertHex = GetAesConversationKey(nsec, npub);
        return await _jsRuntime.InvokeAsync<string>("decryptNostr", secertHex, encryptedContent);
    }
    
    public string EncryptNostrContent(string nsec, string npub, string content)
    {
        var secertHex = GetAesConversationKey(nsec, npub);
            
        return _jsRuntime.InvokeAsync<string>("encryptNostr", secertHex, content)
            .GetAwaiter().GetResult();
    }

    public string DecryptNostrContent(string nsec, string npub, string encryptedContent)
    {
        var secertHex = GetAesConversationKey(nsec, npub);
        return _jsRuntime.InvokeAsync<string>("decryptNostr", secertHex, encryptedContent)
            .GetAwaiter().GetResult();
    }

    private static string GetSharedSecretHexWithoutPrefix(string nsec, string npub)
    {
        var privateKey = new Key(Encoders.Hex.DecodeData(nsec));
        var publicKey = new PubKey("02" + npub);
            
        var secert = publicKey.GetSharedPubkey(privateKey);
        return Encoders.Hex.EncodeData(secert.ToBytes()[1..]);
    }
        
    private static byte[] GetAesConversationKey(string nsec, string npub)
    {
        var privateKey = new Key(Encoders.Hex.DecodeData(nsec));
        var publicKey = new PubKey("02" + npub);
            
        var secert = publicKey.GetSharedPubkey(privateKey);

        return HKDF.Extract(HashAlgorithmName.SHA256, secert.ToBytes()[1..], Encoders.ASCII.DecodeData("nip44-v2"));
    }
}
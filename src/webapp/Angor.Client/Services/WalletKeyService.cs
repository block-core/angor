using System.Security.Cryptography;
using Angor.Shared.Models;
using Microsoft.JSInterop;
using NBitcoin;

namespace Angor.Client.Services;

public class WalletKeyService(IJSRuntime js)
{
    public async Task PrepareAsync(WalletWords words)
    {
        await words.GetOrDeriveExtKeyAsync(async (mnemonic, passphrase) =>
        {
            // Preserve NBitcoin's mnemonic parsing and canonical word spacing.
            string normalized;
            try
            {
                // Wallets created here use English. Avoid loading every language's
                // word list during automatic detection on the browser UI thread.
                normalized = new Mnemonic(mnemonic, Wordlist.English).ToString();
            }
            catch (FormatException)
            {
                normalized = new Mnemonic(mnemonic).ToString();
            }
            byte[] seed = await js.InvokeAsync<byte[]>("deriveWalletSeed", normalized, passphrase);
            try
            {
                var key = ExtKey.CreateFromSeed(seed);
                return key;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        });
    }
}

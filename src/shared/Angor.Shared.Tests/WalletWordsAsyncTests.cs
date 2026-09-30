using System.Security.Cryptography;
using System.Text;
using Angor.Shared;
using Angor.Shared.Models;
using Moq;
using NBitcoin;

namespace Angor.Test;

public class WalletWordsAsyncTests
{
    private const string MnemonicWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    [Theory]
    [InlineData("")]
    [InlineData("TREZOR")]
    [InlineData("é㍍ガバヴァぱばぐゞちぢ十人十色")]
    public async Task AsyncDerivation_MatchesNBitcoinAndPrimesSigningCache(string passphrase)
    {
        using WalletWords words = new() { Words = MnemonicWords, Passphrase = passphrase };
        ExtKey expected = new HdOperations().GetExtendedKey(MnemonicWords, passphrase);
        ExtKey actual = await words.GetOrDeriveExtKeyAsync((mnemonic, password) =>
        {
            byte[] seed = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(mnemonic.Normalize(NormalizationForm.FormKD)),
                Encoding.UTF8.GetBytes(("mnemonic" + password).Normalize(NormalizationForm.FormKD)),
                2048, HashAlgorithmName.SHA512, 64);
            try
            {
                return Task.FromResult(ExtKey.CreateFromSeed(seed));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        });

        Assert.Equal(expected.GetWif(Network.Main).ToString(), actual.GetWif(Network.Main).ToString());
        Assert.Same(actual, words.GetOrDeriveExtKey(Mock.Of<IHdOperations>()));
        Assert.Same(actual, await words.GetOrDeriveExtKeyAsync((_, _) => throw new Exception("Cache was not used")));
        Assert.DoesNotContain(actual.GetWif(Network.Main).ToString(), words.ConvertToString());
    }

    [Fact]
    public async Task AsyncDerivation_WhenDisposedWhileWaiting_DoesNotRetainKey()
    {
        WalletWords words = new() { Words = MnemonicWords };
        TaskCompletionSource<ExtKey> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExtKey> pending = words.GetOrDeriveExtKeyAsync((_, _) => completion.Task);
        words.Dispose();
        completion.SetResult(new HdOperations().GetExtendedKey(MnemonicWords));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.Null(words.CachedExtKey);
    }
}

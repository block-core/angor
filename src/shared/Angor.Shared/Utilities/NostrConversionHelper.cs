using Microsoft.Extensions.Logging;
using Nostr.Client.Utils;
using Nostr.Client.Keys;

namespace Angor.Shared.Utilities
{
    public class NostrConversionHelper
    {
        private readonly ILogger<NostrConversionHelper> _logger;

        public NostrConversionHelper(ILogger<NostrConversionHelper> logger)
        {
            _logger = logger;
        }

        public string? ConvertBech32ToHex(string bech32Key)
        {
            try
            {
                string? hrp;
                return NostrConverter.ToHex(bech32Key, out hrp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error converting Bech32 to hex: {ex.Message}");
                return null;
            }
        }

        public string? ConvertHexToBech32(string hexKey, string prefix)
        {
            try
            {
                return NostrConverter.ToBech32(hexKey, prefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error converting hex to Bech32: {ex.Message}");
                return null;
            }
        }

        public bool IsHexValid(string hexKey)
        {
            return NostrConverter.IsHex(hexKey);
        }

        public string? ConvertHexToNpub(string hexKey)
        {
            return NostrConverter.ToNpub(hexKey);
        }

        public string? ConvertHexToNsec(string hexKey)
        {
            return NostrConverter.ToNsec(hexKey);
        }

        public string? ConvertHexToNote(string hexKey)
        {
            return NostrConverter.ToNote(hexKey);
        }
        
        public string GetPubKeyHexFromPrivateKeyHex(string privateKeyHex)
        {
            try
            {
                return NostrPrivateKey.FromHex(privateKeyHex).DerivePublicKey().Hex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting public key from private key hex: {ex.Message}");
                throw;
            }
        }

        public string GetShortenedNpub(string npub)
        {
            if (string.IsNullOrEmpty(npub) || npub.Length <= 20) return npub;
            return $"{npub.Substring(0, 10)}...{npub.Substring(npub.Length - 10)}";
        }
    }
}

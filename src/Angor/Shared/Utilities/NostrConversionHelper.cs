using Nostr.Client.Utils;  
using System;

namespace Angor.Shared.Utilities
{
    public static class NostrConversionHelper
    {
        /// <summary>
        /// Convert Bech32 key to hex
        /// </summary>
        public static string? ConvertBech32ToHex(string bech32Key)
        {
            try
            {
                string? hrp;
                return NostrConverter.ToHex(bech32Key, out hrp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting Bech32 to hex: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert hex key to Bech32 with a specified prefix
        /// </summary>
        public static string? ConvertHexToBech32(string hexKey, string prefix)
        {
            try
            {
                return NostrConverter.ToBech32(hexKey, prefix);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting hex to Bech32: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if the given hex key is valid
        /// </summary>
        public static bool IsHexValid(string hexKey)
        {
            return NostrConverter.IsHex(hexKey);
        }

        /// <summary>
        /// Convert hex key to npub (Bech32 format)
        /// </summary>
        public static string? ConvertHexToNpub(string hexKey)
        {
            return NostrConverter.ToNpub(hexKey);
        }

        /// <summary>
        /// Convert hex key to nsec (Bech32 format)
        /// </summary>
        public static string? ConvertHexToNsec(string hexKey)
        {
            return NostrConverter.ToNsec(hexKey);
        }

        /// <summary>
        /// Convert hex key to note (Bech32 format)
        /// </summary>
        public static string? ConvertHexToNote(string hexKey)
        {
            return NostrConverter.ToNote(hexKey);
        }
    }
}

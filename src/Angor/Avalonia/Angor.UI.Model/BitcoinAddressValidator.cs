using System.Numerics;
using System.Security.Cryptography;

namespace Angor.UI.Model;

public class BitcoinAddressValidator
{
    public enum BitcoinAddressType
    {
        Unknown,
        P2PKH,    // Pay to Public Key Hash (Legacy)
        P2SH,     // Pay to Script Hash (Legacy)
        P2WPKH,   // Pay to Witness Public Key Hash (Native SegWit)
        P2WSH     // Pay to Witness Script Hash (Native SegWit)
    }

    private static readonly string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private static readonly string Bech32Alphabet = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    public static ValidationResult ValidateBitcoinAddress(string address, BitcoinNetwork expectedNetwork)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new ValidationResult(false, "Address cannot be empty");
        }

        // Check if it's a Bech32 address
        if (address.StartsWith("bc1") || address.StartsWith("tb1"))
        {
            return ValidateBech32Address(address, expectedNetwork);
        }

        // Legacy or P2SH address validation
        return ValidateLegacyAddress(address, expectedNetwork);
    }

    private static ValidationResult ValidateLegacyAddress(string address, BitcoinNetwork expectedNetwork)
    {
        // Validate length for legacy addresses
        if (address.Length < 26 || address.Length > 35)
        {
            return new ValidationResult(false, "Invalid legacy address length");
        }

        // Validate characters
        var invalidChars = address.Where(c => !Base58Alphabet.Contains(c)).ToList();
        if (invalidChars.Any())
        {
            return new ValidationResult(false, 
                $"Invalid characters in address: {string.Join(", ", invalidChars)}");
        }

        try
        {
            // Decode Base58
            var decoded = Base58Decode(address);
            
            // Validate decoded length
            if (decoded.Length != 25)
            {
                return new ValidationResult(false, 
                    $"Invalid decoded length: {decoded.Length} bytes (expected 25 bytes)");
            }

            // Separate components
            var withoutChecksum = decoded.Take(21).ToArray();
            var checksum = decoded.Skip(21).Take(4).ToArray();

            // Calculate checksum
            var calculatedChecksum = CalculateChecksum(withoutChecksum);

            // Compare checksums
            if (!checksum.SequenceEqual(calculatedChecksum))
            {
                return new ValidationResult(false, "Invalid checksum");
            }

            // Identify version and network
            var version = decoded[0];
            var (detectedNetwork, addressType) = DetermineNetworkAndType(version);

            if (detectedNetwork == BitcoinNetwork.Unknown)
            {
                return new ValidationResult(false, 
                    $"Unrecognized version byte: 0x{version:X2}", 
                    detectedNetwork, 
                    addressType);
            }

            // Validate network
            if (expectedNetwork != detectedNetwork)
            {
                return new ValidationResult(false,
                    $"Address belongs to network {detectedNetwork} but expected {expectedNetwork}",
                    detectedNetwork,
                    addressType);
            }

            return new ValidationResult(true, "Valid address", detectedNetwork, addressType);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Error processing address: {ex.Message}");
        }
    }

    private static ValidationResult ValidateBech32Address(string address, BitcoinNetwork expectedNetwork)
    {
        try
        {
            // Basic Bech32 validation
            if (address.Length < 14 || address.Length > 74)
            {
                return new ValidationResult(false, "Invalid Bech32 address length");
            }

            // Validate characters
            var invalidChars = address.ToLower()
                .Skip(4) // Skip the prefix (bc1 or tb1) and separator (1)
                .Where(c => !Bech32Alphabet.Contains(c))
                .ToList();

            if (invalidChars.Any())
            {
                return new ValidationResult(false, 
                    $"Invalid characters in Bech32 address: {string.Join(", ", invalidChars)}");
            }

            // Determine network from prefix
            var detectedNetwork = address.ToLower().StartsWith("bc1") 
                ? BitcoinNetwork.Mainnet 
                : address.ToLower().StartsWith("tb1") 
                    ? BitcoinNetwork.Testnet 
                    : BitcoinNetwork.Unknown;

            if (detectedNetwork == BitcoinNetwork.Unknown)
            {
                return new ValidationResult(false, 
                    "Invalid Bech32 address prefix", 
                    BitcoinNetwork.Unknown, 
                    BitcoinAddressType.Unknown);
            }

            // Validate network
            if (expectedNetwork != detectedNetwork)
            {
                return new ValidationResult(false,
                    $"Address belongs to network {detectedNetwork} but expected {expectedNetwork}",
                    detectedNetwork,
                    BitcoinAddressType.Unknown);
            }

            // Determine SegWit version and program length
            var program = DecodeBech32(address);
            var witnessVersion = program[0];
            var dataLength = program.Length - 1;

            // Validate according to BIP141
            var addressType = DetermineSegWitType(witnessVersion, dataLength);
            if (addressType == BitcoinAddressType.Unknown)
            {
                return new ValidationResult(false, 
                    "Invalid SegWit program length", 
                    detectedNetwork, 
                    BitcoinAddressType.Unknown);
            }

            return new ValidationResult(true, "Valid SegWit address", detectedNetwork, addressType);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Error processing Bech32 address: {ex.Message}");
        }
    }

    private static BitcoinAddressType DetermineSegWitType(int witnessVersion, int programLength)
    {
        if (witnessVersion != 0) return BitcoinAddressType.Unknown;

        return programLength switch
        {
            20 => BitcoinAddressType.P2WPKH,
            32 => BitcoinAddressType.P2WSH,
            _ => BitcoinAddressType.Unknown
        };
    }

    private static (BitcoinNetwork network, BitcoinAddressType type) DetermineNetworkAndType(byte version)
    {
        return version switch
        {
            0x00 => (BitcoinNetwork.Mainnet, BitcoinAddressType.P2PKH), // Mainnet P2PKH (1...)
            0x05 => (BitcoinNetwork.Mainnet, BitcoinAddressType.P2SH),  // Mainnet P2SH (3...)
            0x6F => (BitcoinNetwork.Testnet, BitcoinAddressType.P2PKH), // Testnet P2PKH (m... or n...)
            0xC4 => (BitcoinNetwork.Testnet, BitcoinAddressType.P2SH),  // Testnet P2SH (2...)
            0x3C => (BitcoinNetwork.Regtest, BitcoinAddressType.P2PKH), // Regtest P2PKH
            0x26 => (BitcoinNetwork.Regtest, BitcoinAddressType.P2SH),  // Regtest P2SH
            _ => (BitcoinNetwork.Unknown, BitcoinAddressType.Unknown)
        };
    }

    private static byte[] Base58Decode(string base58)
    {
        var result = new BigInteger(0);
        var multiplier = new BigInteger(1);

        // Convert from Base58 to number
        for (var i = base58.Length - 1; i >= 0; i--)
        {
            var digit = Base58Alphabet.IndexOf(base58[i]);
            if (digit == -1)
            {
                throw new FormatException($"Invalid character '{base58[i]}' at position {i}");
            }

            result += multiplier * digit;
            multiplier *= Base58Alphabet.Length;
        }

        // Convert to bytes
        var bytes = result.ToByteArray().Reverse().SkipWhile(b => b == 0).ToArray();

        // Add leading zeros as needed
        var leadingZeros = base58.TakeWhile(c => c == '1').Count();
        var leadingZeroBytes = Enumerable.Repeat((byte)0, leadingZeros).ToArray();

        return leadingZeroBytes.Concat(bytes).ToArray();
    }

    private static byte[] DecodeBech32(string address)
    {
        // Note: This is a simplified Bech32 decoder
        // In a production environment, you should implement full Bech32 decoding
        // including checksum verification according to BIP173
        var data = address.ToLower()
            .Skip(4) // Skip prefix and separator
            .Select(c => (byte)Bech32Alphabet.IndexOf(c))
            .ToArray();

        return data;
    }

    private static byte[] CalculateChecksum(byte[] data)
    {
        using (var sha256 = SHA256.Create())
        {
            var hash1 = sha256.ComputeHash(data);
            var hash2 = sha256.ComputeHash(hash1);
            return hash2.Take(4).ToArray();
        }
    }

    public class ValidationResult
    {
        public ValidationResult(bool isValid, string message, BitcoinNetwork network = BitcoinNetwork.Unknown,
            BitcoinAddressType addressType = BitcoinAddressType.Unknown)
        {
            IsValid = isValid;
            Message = message;
            Network = network;
            AddressType = addressType;
        }

        public bool IsValid { get; }
        public string Message { get; }
        public BitcoinNetwork Network { get; init; }
        public BitcoinAddressType AddressType { get; init; }
    }
}
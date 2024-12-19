using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace AngorApp.Sections.Wallet;

public class BitcoinAddressValidator
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public BitcoinNetwork Network { get; set; }
        public BitcoinAddressType AddressType { get; set; }

        public ValidationResult(bool isValid, string message, BitcoinNetwork network = BitcoinNetwork.Unknown, 
            BitcoinAddressType addressType = BitcoinAddressType.Unknown)
        {
            IsValid = isValid;
            Message = message;
            Network = network;
            AddressType = addressType;
        }
    }

    public enum BitcoinNetwork
    {
        Unknown,
        Mainnet,
        Testnet,
        Regtest
    }

    public enum BitcoinAddressType
    {
        Unknown,
        P2PKH,  // Pay to Public Key Hash
        P2SH    // Pay to Script Hash
    }

    private static readonly string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static ValidationResult ValidateBitcoinAddress(string address, BitcoinNetwork expectedNetwork)
    {
        if (string.IsNullOrEmpty(address))
            return new ValidationResult(false, "La dirección no puede estar vacía");

        // Validar longitud
        if (address.Length < 26 || address.Length > 35)
            return new ValidationResult(false, "Longitud de dirección inválida");

        // Validar caracteres
        if (!address.All(c => Alphabet.Contains((char)c)))
            return new ValidationResult(false, "Caracteres no válidos en la dirección");

        try
        {
            // Decodificar Base58
            byte[] decoded = Base58Decode(address);
            if (decoded.Length < 25)
                return new ValidationResult(false, "Dirección demasiado corta después de decodificar");

            // Separar los componentes
            byte[] withoutChecksum = decoded.Take(decoded.Length - 4).ToArray();
            byte[] checksum = decoded.Skip(decoded.Length - 4).Take(4).ToArray();

            // Calcular checksum
            byte[] calculatedChecksum = CalculateChecksum(withoutChecksum);

            // Comparar checksums
            if (!checksum.SequenceEqual(calculatedChecksum))
                return new ValidationResult(false, "Checksum inválido");

            // Identificar versión y red
            byte version = decoded[0];
            var (detectedNetwork, addressType) = DetermineNetworkAndType(version);

            if (detectedNetwork == BitcoinNetwork.Unknown)
                return new ValidationResult(false, "Versión de dirección no reconocida", detectedNetwork, addressType);

            // Validar que la red coincida con la esperada
            if (expectedNetwork != detectedNetwork)
                return new ValidationResult(false, 
                    $"La dirección pertenece a la red {detectedNetwork} pero se esperaba {expectedNetwork}", 
                    detectedNetwork, 
                    addressType);

            return new ValidationResult(true, "Dirección válida", detectedNetwork, addressType);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Error al procesar la dirección: {ex.Message}");
        }
    }

    // Sobrecarga del método original para mantener compatibilidad
    public static ValidationResult ValidateBitcoinAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return new ValidationResult(false, "La dirección no puede estar vacía");

        try
        {
            // Intentar detectar la red basándose en el primer carácter
            BitcoinNetwork presumedNetwork = address[0] switch
            {
                '1' or '3' => BitcoinNetwork.Mainnet,
                'm' or 'n' or '2' => BitcoinNetwork.Testnet,
                _ => BitcoinNetwork.Unknown
            };

            return ValidateBitcoinAddress(address, presumedNetwork);
        }
        catch
        {
            return ValidateBitcoinAddress(address, BitcoinNetwork.Unknown);
        }
    }

    private static (BitcoinNetwork network, BitcoinAddressType type) DetermineNetworkAndType(byte version)
    {
        return version switch
        {
            0x00 => (BitcoinNetwork.Mainnet, BitcoinAddressType.P2PKH),  // Mainnet P2PKH (1...)
            0x05 => (BitcoinNetwork.Mainnet, BitcoinAddressType.P2SH),   // Mainnet P2SH (3...)
            0x6F => (BitcoinNetwork.Testnet, BitcoinAddressType.P2PKH),  // Testnet P2PKH (m... or n...)
            0xC4 => (BitcoinNetwork.Testnet, BitcoinAddressType.P2SH),   // Testnet P2SH (2...)
            0x3C => (BitcoinNetwork.Regtest, BitcoinAddressType.P2PKH),  // Regtest P2PKH
            0x26 => (BitcoinNetwork.Regtest, BitcoinAddressType.P2SH),   // Regtest P2SH
            _ => (BitcoinNetwork.Unknown, BitcoinAddressType.Unknown)
        };
    }

    private static byte[] Base58Decode(string base58)
    {
        var result = new BigInteger(0);
        var multiplier = new BigInteger(1);

        // Convertir de Base58 a número
        for (var i = base58.Length - 1; i >= 0; i--)
        {
            var digit = Alphabet.IndexOf(base58[i]);
            if (digit == -1)
                throw new FormatException($"Carácter inválido '{base58[i]}' en la posición {i}");

            result += multiplier * digit;
            multiplier *= Alphabet.Length;
        }

        // Convertir a bytes
        var bytes = result.ToByteArray().Reverse().SkipWhile(b => b == 0).ToArray();

        // Agregar ceros iniciales según sea necesario
        var leadingZeros = base58.TakeWhile(c => c == '1').Count();
        var leadingZeroBytes = Enumerable.Repeat((byte)0, leadingZeros).ToArray();

        return leadingZeroBytes.Concat(bytes).ToArray();
    }

    private static byte[] CalculateChecksum(byte[] data)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hash1 = sha256.ComputeHash(data);
            byte[] hash2 = sha256.ComputeHash(hash1);
            return hash2.Take(4).ToArray();
        }
    }
}
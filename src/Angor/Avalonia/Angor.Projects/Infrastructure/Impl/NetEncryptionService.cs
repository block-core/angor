using System.Security.Cryptography;
using System.Text;
using Angor.Client.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;

namespace Angor.Projects.Infrastructure.Impl;

public class NetEncryptionService : IEncryptionService
{
    public Task<string> EncryptData(string secretData, string password)
    {
        // Generar salt y vector de inicialización aleatorios
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] iv = RandomNumberGenerator.GetBytes(12);
            
        // Derivar clave con PBKDF2 (250000 iteraciones, SHA-256)
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] key;
            
        using (var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, 250000, HashAlgorithmName.SHA256))
        {
            key = pbkdf2.GetBytes(32); // 256 bits
        }
            
        // Encriptar usando AES-GCM
        byte[] encryptedData;
            
        using (var aesGcm = new AesGcm(key, 16))
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(secretData);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[16]; // Tag de autenticación
                
            aesGcm.Encrypt(iv, plaintext, ciphertext, tag);
                
            // Combinar ciphertext + tag
            encryptedData = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, encryptedData, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, encryptedData, ciphertext.Length, tag.Length);
        }
            
        // Combinar salt + iv + datos encriptados
        byte[] resultBytes = new byte[salt.Length + iv.Length + encryptedData.Length];
        Buffer.BlockCopy(salt, 0, resultBytes, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, resultBytes, salt.Length, iv.Length);
        Buffer.BlockCopy(encryptedData, 0, resultBytes, salt.Length + iv.Length, encryptedData.Length);
            
        return Task.FromResult(Convert.ToBase64String(resultBytes));
    }

    public Task<string> DecryptData(string encryptedData, string password)
    {
        // Decodificar datos
        byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
            
        // Extraer salt, iv y datos encriptados
        byte[] salt = new byte[16];
        byte[] iv = new byte[12];
        byte[] data = new byte[encryptedBytes.Length - salt.Length - iv.Length];
            
        Buffer.BlockCopy(encryptedBytes, 0, salt, 0, salt.Length);
        Buffer.BlockCopy(encryptedBytes, salt.Length, iv, 0, iv.Length);
        Buffer.BlockCopy(encryptedBytes, salt.Length + iv.Length, data, 0, data.Length);
            
        // Derivar clave con PBKDF2
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] key;
            
        using (var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, 250000, HashAlgorithmName.SHA256))
        {
            key = pbkdf2.GetBytes(32); // 256 bits
        }
            
        // Dividir datos y tag de autenticación
        byte[] ciphertext = new byte[data.Length - 16];
        byte[] tag = new byte[16];
            
        Buffer.BlockCopy(data, 0, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(data, ciphertext.Length, tag, 0, tag.Length);
            
        // Desencriptar usando AES-GCM
        byte[] plaintext = new byte[ciphertext.Length];
            
        using (var aesGcm = new AesGcm(key, 16))
        {
            aesGcm.Decrypt(iv, ciphertext, tag, plaintext);
        }
            
        return Task.FromResult(Encoding.UTF8.GetString(plaintext));
    }

    public Task<string> EncryptNostrContentAsync(string nsec, string npub, string content)
    {
        var secretHex = GetSharedSecretHexWithoutPrefix(nsec, npub);
        byte[] sharedSecret = Encoders.Hex.DecodeData(secretHex);
            
        // Encriptar usando AES-CBC (como en el JS)
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Key = sharedSecret;
        aes.GenerateIV(); // Genera IV de 16 bytes
            
        ICryptoTransform encryptor = aes.CreateEncryptor();
            
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        byte[] encryptedBytes;
            
        using (var ms = new MemoryStream())
        {
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(contentBytes, 0, contentBytes.Length);
                cs.FlushFinalBlock();
            }
            encryptedBytes = ms.ToArray();
        }
            
        // Formato: encryptedBase64?iv=ivBase64
        string encryptedBase64 = Convert.ToBase64String(encryptedBytes);
        string ivBase64 = Convert.ToBase64String(aes.IV);
            
        return Task.FromResult($"{encryptedBase64}?iv={ivBase64}");
    }

    public Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
    {
        var secretHex = GetSharedSecretHexWithoutPrefix(nsec, npub);
        byte[] sharedSecret = Encoders.Hex.DecodeData(secretHex);
            
        // Dividir ciphertext e IV
        string[] parts = encryptedContent.Split("?iv=");
        byte[] ciphertext = Convert.FromBase64String(parts[0]);
        byte[] iv = Convert.FromBase64String(parts[1]);
            
        // Desencriptar usando AES-CBC
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Key = sharedSecret;
        aes.IV = iv;
            
        ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] decryptedBytes;
            
        using (var ms = new MemoryStream(ciphertext))
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
        using (var resultMs = new MemoryStream())
        {
            cs.CopyTo(resultMs);
            decryptedBytes = resultMs.ToArray();
        }
            
        return Task.FromResult(Encoding.UTF8.GetString(decryptedBytes));
    }

    private static string GetSharedSecretHexWithoutPrefix(string nsec, string npub)
    {
        var privateKey = new Key(Encoders.Hex.DecodeData(nsec));
        var publicKey = new PubKey("02" + npub);
            
        var secert = publicKey.GetSharedPubkey(privateKey);
        return Encoders.Hex.EncodeData(secert.ToBytes()[1..]);
    }
}
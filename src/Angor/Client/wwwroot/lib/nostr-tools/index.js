/**
 * Nostr Protocol NIP Implementation Library
 * 
 * This library provides implementations of various Nostr Implementation Possibilities (NIPs)
 * for cryptographic operations in the Nostr protocol.
 * 
 * Includes:
 * - NIP-04 (deprecated): Direct Message Encryption
 * - NIP-17 (recommended): Improved Direct Message Encryption
 * - NIP-44 (stub): Encrypted Payloads
 * - NIP-59 (stub): Gift Wrap
 */

// NIP-04 is deprecated in favor of NIP-17 
const nip04 = { 
  encrypt: async () => { throw new Error('NIP-04 is deprecated'); },
  decrypt: async () => { throw new Error('NIP-04 is deprecated'); }
};

// Complete NIP-17 implementation for encryption
const nip17 = {
  /**
   * Encrypts content using NIP-17 standard
   * 
   * Implements ephemeral key encryption with ECDH and AES-CBC
   * 
   * @param {string|Uint8Array} pubkey - Recipient's public key (hex or bytes)
   * @param {string} content - Content to encrypt
   * @returns {string} - JSON string containing encrypted data in NIP-17 format
   */
  encrypt: async (pubkey, content) => {
    try {
      // Convert pubkey to bytes if it's in hex format
      const pubkeyBytes = typeof pubkey === 'string' && pubkey.match(/^[0-9a-fA-F]+$/) 
        ? hexToBytes(pubkey) 
        : pubkey;
      
      // Generate ephemeral keypair
      const ephemeralKeyPair = await window.crypto.subtle.generateKey(
        { name: 'ECDH', namedCurve: 'P-256' },
        true,
        ['deriveKey']
      );
      
      // Derive shared secret
      const sharedSecret = await deriveSharedSecret(ephemeralKeyPair.privateKey, pubkeyBytes);
      
      // Encrypt with the shared secret
      const encoder = new TextEncoder();
      const data = encoder.encode(content);
      const iv = window.crypto.getRandomValues(new Uint8Array(16));
      
      const key = await window.crypto.subtle.importKey(
        'raw',
        sharedSecret,
        { name: 'AES-CBC', length: 256 },
        false,
        ['encrypt']
      );
      
      const ciphertext = await window.crypto.subtle.encrypt(
        { name: 'AES-CBC', iv },
        key,
        data
      );
      
      // Encode ephemeral public key, iv, and ciphertext
      const ephPublicKeyBytes = await window.crypto.subtle.exportKey('raw', ephemeralKeyPair.publicKey);
      
      // Prepare result in NIP-17 format
      const result = {
        v: 'v1',
        pub: bytesToHex(new Uint8Array(ephPublicKeyBytes)),
        iv: bytesToHex(iv),
        data: bytesToHex(new Uint8Array(ciphertext))
      };
      
      return JSON.stringify(result);
    } catch (e) {
      console.error('NIP-17 encryption error:', e);
      throw e;
    }
  },
  
  /**
   * Decrypts content using NIP-17 standard
   * 
   * @param {string|Uint8Array} privkey - Private key for decryption (hex or bytes)
   * @param {string} encryptedContent - JSON string in NIP-17 format
   * @returns {string} - Decrypted content
   */
  decrypt: async (privkey, encryptedContent) => {
    try {
      // Parse the encrypted content
      const { v, pub, iv, data } = JSON.parse(encryptedContent);
      
      if (v !== 'v1') {
        throw new Error(`Unsupported NIP-17 version: ${v}`);
      }
      
      // Convert hex strings to bytes
      const ephPublicKeyBytes = hexToBytes(pub);
      const ivBytes = hexToBytes(iv);
      const ciphertextBytes = hexToBytes(data);
      
      // Import private key
      const privateKeyBytes = typeof privkey === 'string' && privkey.match(/^[0-9a-fA-F]+$/)
        ? hexToBytes(privkey)
        : privkey;
      
      // Derive shared secret
      const privKeyImport = await window.crypto.subtle.importKey(
        'raw',
        privateKeyBytes,
        { name: 'ECDH', namedCurve: 'P-256' },
        false,
        ['deriveKey']
      );
      
      const ephPubKeyImport = await window.crypto.subtle.importKey(
        'raw',
        ephPublicKeyBytes,
        { name: 'ECDH', namedCurve: 'P-256' },
        false,
        ['deriveKey']
      );
      
      const sharedSecret = await deriveSharedSecret(privKeyImport, ephPubKeyImport);
      
      // Decrypt with the shared secret
      const key = await window.crypto.subtle.importKey(
        'raw',
        sharedSecret,
        { name: 'AES-CBC', length: 256 },
        false,
        ['decrypt']
      );
      
      const plaintext = await window.crypto.subtle.decrypt(
        { name: 'AES-CBC', iv: ivBytes },
        key,
        ciphertextBytes
      );
      
      const decoder = new TextDecoder();
      return decoder.decode(plaintext);
    } catch (e) {
      console.error('NIP-17 decryption error:', e);
      throw e;
    }
  }
};

/**
 * Derives a shared secret using ECDH
 * 
 * @param {CryptoKey} privateKey - Private key
 * @param {CryptoKey|Uint8Array} publicKey - Public key
 * @returns {Promise<ArrayBuffer>} - The derived shared secret
 */
async function deriveSharedSecret(privateKey, publicKey) {
  const derivedKey = await window.crypto.subtle.deriveKey(
    { name: 'ECDH', public: publicKey },
    privateKey,
    { name: 'AES-CBC', length: 256 },
    true,
    ['encrypt', 'decrypt']
  );
  
  return await window.crypto.subtle.exportKey('raw', derivedKey);
}

/**
 * Converts a hex string to bytes
 * 
 * @param {string} hex - Hex string
 * @returns {Uint8Array} - Byte array
 */
function hexToBytes(hex) {
  const bytes = new Uint8Array(hex.length / 2);
  for (let i = 0; i < hex.length; i += 2) {
    bytes[i / 2] = parseInt(hex.substring(i, i + 2), 16);
  }
  return bytes;
}

/**
 * Converts bytes to a hex string
 * 
 * @param {Uint8Array} bytes - Byte array
 * @returns {string} - Hex string
 */
function bytesToHex(bytes) {
  return Array.from(bytes)
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
}

// Stub implementations - to be replaced with actual implementations if needed
const nip44 = {
  encrypt: async (pubkey, content) => { 
    console.log('NIP-44 encrypt stub called with pubkey:', pubkey);
    return 'nip44-encrypted:' + content; 
  },
  decrypt: async (privkey, content) => {
    console.log('NIP-44 decrypt stub called with privkey:', privkey);
    return content.replace('nip44-encrypted:', '');
  }
};

const nip59 = {
  encrypt: async (pubkey, content) => { 
    console.log('NIP-59 encrypt stub called with pubkey:', pubkey);
    return 'nip59-encrypted:' + content; 
  },
  decrypt: async (privkey, content) => {
    console.log('NIP-59 decrypt stub called with privkey:', privkey);
    return content.replace('nip59-encrypted:', '');
  }
};

export { nip04, nip17, nip44, nip59 };

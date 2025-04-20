import { nip17 } from "./lib/nostr-tools/index.js";

function hexToBytes(hex) {
    let bytes = [];
    for (let c = 0; c < hex.length; c += 2) {
        bytes.push(parseInt(hex.substr(c, 2), 16));
    }
    return new Uint8Array(bytes);
}

function bytesToHex(bytes) {
    return Array.from(bytes)
        .map(b => b.toString(16).padStart(2, '0'))
        .join('');
}

/**
 * Converts a shared secret to a public key for encryption
 * 
 * Uses HKDF (Hash-based Key Derivation Function) to derive a secure key
 * for use with the NIP-17 encryption standard
 * 
 * @param {string} sharedSecretHex - Shared secret in hex format
 * @returns {string} - Public key for encryption in hex format
 */
async function sharedSecretToEncryptionKey(sharedSecretHex) {
    // Convert hex to bytes
    const sharedSecretBytes = hexToBytes(sharedSecretHex);
    
    // Step 1: Import the shared secret as raw key material
    const keyMaterial = await window.crypto.subtle.importKey(
        "raw",
        sharedSecretBytes,
        { name: "HKDF" },
        false,
        ["deriveBits"]
    );
    
    // Step 2: Use HKDF to derive bits for the encryption key
    // According to WebCrypto standards, HKDF requires hash, salt, and info
    const derivedBits = await window.crypto.subtle.deriveBits(
        {
            name: "HKDF",
            hash: "SHA-256",
            salt: new Uint8Array(32), // Fixed salt
            info: new TextEncoder().encode("nostr-encryption-key")
        },
        keyMaterial,
        256 // 256 bits for AES-256
    );
    
    // Step 3: Convert derived bits to hex format
    return bytesToHex(new Uint8Array(derivedBits));
}

/**
 * Converts a shared secret to a private key for decryption
 * 
 * Uses HKDF (Hash-based Key Derivation Function) to derive a secure key
 * for use with the NIP-17 decryption standard
 * 
 * @param {string} sharedSecretHex - Shared secret in hex format
 * @returns {string} - Private key for decryption in hex format
 */
async function sharedSecretToDecryptionKey(sharedSecretHex) {
    // Convert hex to bytes
    const sharedSecretBytes = hexToBytes(sharedSecretHex);
    
    // Step 1: Import the shared secret as raw key material
    const keyMaterial = await window.crypto.subtle.importKey(
        "raw",
        sharedSecretBytes,
        { name: "HKDF" },
        false,
        ["deriveBits"]
    );
    
    // Step 2: Use HKDF to derive bits for the decryption key
    // According to WebCrypto standards, HKDF requires hash, salt, and info
    const derivedBits = await window.crypto.subtle.deriveBits(
        {
            name: "HKDF",
            hash: "SHA-256",
            salt: new Uint8Array(32), // Fixed salt
            info: new TextEncoder().encode("nostr-decryption-key")
        },
        keyMaterial,
        256 // 256 bits for AES-256
    );
    
    // Step 3: Convert derived bits to hex format
    return bytesToHex(new Uint8Array(derivedBits));
}

/**
 * Encryption function using modern encryption standards
 * 
 * @param {string} sharedSecretHex - The shared secret in hex format
 * @param {string} message - The message to encrypt
 * @returns {string} - The encrypted message
 */
async function encryptNostr(sharedSecretHex, message) {
    console.warn("Using improved encryption standard (NIP-17)");
    
    try {
        // Convert shared secret to a proper public key
        const pubkey = await sharedSecretToEncryptionKey(sharedSecretHex);
        
        // Use the encryption with the derived key
        return await nip17.encrypt(pubkey, message);
    } catch (e) {
        console.error("Error in encryption:", e);
        throw e;
    }
}

/**
 * Decryption function using modern encryption standards
 * 
 * @param {string} sharedSecretHex - The shared secret in hex format
 * @param {string} encryptedMessage - The encrypted message to decrypt
 * @returns {string} - The decrypted message
 */
async function decryptNostr(sharedSecretHex, encryptedMessage) {
    console.warn("Using improved decryption standard (NIP-17)");
    
    try {
        // Convert shared secret to a proper private key
        const privkey = await sharedSecretToDecryptionKey(sharedSecretHex);
        
        // Use the decryption with the derived key
        return await nip17.decrypt(privkey, encryptedMessage);
    } catch (e) {
        console.error("Error in decryption:", e);
        throw e;
    }
}

// Export the functions to window
window.encryptNostr = encryptNostr;
window.decryptNostr = decryptNostr;
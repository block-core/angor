import { nip17 } from './lib/nostr-tools/index.js';

/**
 * NIP-17 Encryption Module
 * 
 * Provides implementation of the Nostr NIP-17 standard for encryption
 * which replaces the deprecated NIP-04 standard. This module creates
 * global window objects that can be called from Blazor using JSInterop.
 */

/**
 * Encrypts a message using NIP-17 standard
 * 
 * @param {string} pubkey - The recipient's public key
 * @param {string} content - The content to encrypt
 * @returns {Promise<string>} - The encrypted content in NIP-17 format
 */
async function nip17Encrypt(pubkey, content) {
  try {
    return await nip17.encrypt(pubkey, content);
  } catch (e) {
    console.error('Error in nip17Encrypt:', e);
    throw e;
  }
}

/**
 * Decrypts a message using NIP-17 standard
 * 
 * @param {string} privkey - The private key for decryption
 * @param {string} encryptedContent - The encrypted content in NIP-17 format
 * @returns {Promise<string>} - The decrypted content
 */
async function nip17Decrypt(privkey, encryptedContent) {
  try {
    return await nip17.decrypt(privkey, encryptedContent);
  } catch (e) {
    console.error('Error in nip17Decrypt:', e);
    throw e;
  }
}

// Expose functions to the global window object for JSInterop access
window.nip17Encrypt = nip17Encrypt;
window.nip17Decrypt = nip17Decrypt; 
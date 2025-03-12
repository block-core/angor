import { Base64 } from "./lib/js-base64/base64.mjs";
import { nip44 } from "@nostr/tools";

function hexToBytes(hex) {
    let bytes = [];
    for (let c = 0; c < hex.length; c += 2) {
        bytes.push(parseInt(hex.substr(c, 2), 16));
    }
    return new Uint8Array(bytes);
}

async function encryptNostr(privateKeyHex, recipientPublicKeyHex, message) {
    try {
        const sharedSecret = nip44.getConversationKey(privateKeyHex, recipientPublicKeyHex);

        const encoder = new TextEncoder();
        const data = encoder.encode(message);

        const iv = crypto.getRandomValues(new Uint8Array(12)); // NIP-44 recommends 12-byte nonce for ChaCha20

        const key = await crypto.subtle.importKey('raw', sharedSecret, { name: 'AES-GCM' }, false, ['encrypt']);

        const encrypted = await crypto.subtle.encrypt({
            name: 'AES-GCM',
            iv: iv
        }, key, data);

        const encryptedBase64 = Base64.fromUint8Array(new Uint8Array(encrypted));
        const ivBase64 = Base64.fromUint8Array(iv);

        return `${encryptedBase64}?iv=${ivBase64}`;
    } catch (e) {
        console.error(e);
        throw e;
    }
}

async function decryptNostr(privateKeyHex, senderPublicKeyHex, encryptedMessage) {
    try {
        const sharedSecret = nip44.getConversationKey(privateKeyHex, senderPublicKeyHex);

        let [ctb64, ivb64] = encryptedMessage.split("?iv=");

        const iv = Base64.toUint8Array(ivb64);
        const ciphertext = Base64.toUint8Array(ctb64);

        const key = await crypto.subtle.importKey('raw', sharedSecret, { name: 'AES-GCM' }, false, ['decrypt']);

        const decrypted = await crypto.subtle.decrypt({
            name: 'AES-GCM',
            iv: iv
        }, key, ciphertext);

        const decoder = new TextDecoder();
        return decoder.decode(decrypted);
    } catch (e) {
        console.error(e);
        throw e;
    }
}

window.encryptNostr = encryptNostr;
window.decryptNostr = decryptNostr;

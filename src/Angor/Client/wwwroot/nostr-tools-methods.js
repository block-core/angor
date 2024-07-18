import {Base64} from "./lib/js-base64/base64.mjs";
function hexToBytes(hex) {
    let bytes = [];
    for (let c = 0; c < hex.length; c += 2) {
        bytes.push(parseInt(hex.substr(c, 2), 16));
    }
    return new Uint8Array(bytes);
}

async function encryptNostr(sharedSecretHex, message) {
    try {
        const sharedSecret = hexToBytes(sharedSecretHex);

        const encoder = new TextEncoder();
        const data = encoder.encode(message);

        const iv = crypto.getRandomValues(new Uint8Array(16)); // Initialization vector for AES-CBC

        const key = await crypto.subtle.importKey('raw', sharedSecret, 'AES-CBC', false, ['encrypt']);

        const encrypted = await crypto.subtle.encrypt({
            name: 'AES-CBC',
            iv: iv
        }, key, data);

        const encryptedBase64 = Base64.fromUint8Array(new Uint8Array(encrypted));
        const ivBase64 = Base64.fromUint8Array(iv);

        return  `${encryptedBase64}?iv=${ivBase64}`;
    } catch (e) {
        console.log(e)
        throw e;
    }
}

async function decryptNostr(sharedSecretHex, encryptedMessage) {
    try {
        const sharedSecret = hexToBytes(sharedSecretHex);

        let [ctb64, ivb64] = encryptedMessage.split("?iv=")

        const iv = Base64.toUint8Array(ivb64);
        const ciphertext = Base64.toUint8Array(ctb64);

        const key = await crypto.subtle.importKey('raw', sharedSecret, 'AES-CBC', false, ['decrypt']);

        const decrypted = await crypto.subtle.decrypt({
            name: 'AES-CBC',
            iv: iv
        }, key, ciphertext);

        const decoder = new TextDecoder();
        return  decoder.decode(decrypted);
    }
    catch (e)
    {
        console.log(e)
        throw e;
    }
}

window.encryptNostr = encryptNostr;
window.decryptNostr = decryptNostr;
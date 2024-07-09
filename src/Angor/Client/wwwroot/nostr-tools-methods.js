import {Base64} from "./lib/js-base64/base64.mjs";
function hexToBytes(hex) {
    let bytes = [];
    for (let c = 0; c < hex.length; c += 2) {
        bytes.push(parseInt(hex.substr(c, 2), 16));
    }
    return new Uint8Array(bytes);
}

function bytesToHex(bytes) {
    return Array.prototype.map.call(bytes, x => ('00' + x.toString(16)).slice(-2)).join('');
}


// async function importKey(pem, isPrivate) {
//     const binaryDer = str2ab(atob(pem));
//     return crypto.subtle.importKey(
//         'pkcs8',
//         binaryDer,
//         {
//             name: 'ECDH',
//             namedCurve: 'P-256',
//         },
//         true,
//         isPrivate ? ['deriveBits'] : []
//     );
// }

// function str2ab(str) {
//     const buf = new ArrayBuffer(str.length);
//     const bufView = new Uint8Array(buf);
//     for (let i = 0, strLen = str.length; i < strLen; i++) {
//         bufView[i] = str.charCodeAt(i);
//     }
//     return buf;
// }
//
// async function deriveSharedSecret(privateKey, publicKey) {
//     const sharedSecretBits = await crypto.subtle.deriveBits(
//         {
//             name: 'ECDH',
//             public: publicKey,
//         },
//         privateKey,
//         256
//     );
//
//     return crypto.subtle.importKey(
//         'raw',
//         sharedSecretBits,
//         {
//             name: 'AES-CBC',
//             length: 256,
//         },
//         true,
//         ['encrypt', 'decrypt']
//     );
// }

async function encryptNostr(sharedSecretHex, message) {
    const sharedSecret = hexToBytes(sharedSecretHex);

    const encoder = new TextEncoder();
    const data = encoder.encode(message);

    const iv = crypto.getRandomValues(new Uint8Array(16)); // Initialization vector for AES-CBC

    const key = await crypto.subtle.importKey('raw', sharedSecret, 'AES-CBC', false, ['encrypt']);

    const encrypted = await crypto.subtle.encrypt({
        name: 'AES-CBC',
        iv: iv
    }, key, data);

    return bytesToHex(new Uint8Array(encrypted)) + "?iv=" + bytesToHex(iv);
}

function ab2str(buf) {
    return btoa(String.fromCharCode.apply(null, new Uint8Array(buf)));
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
        const decryptedMessage = decoder.decode(decrypted);

        console.log('Decrypted Message:', decryptedMessage);
        return decryptedMessage;
    }
    catch (e)
    {
        console.log(e)
        throw e;
    }
}

window.encryptNostr = encryptNostr;
window.decryptNostr = decryptNostr;
    
console.log(window.encryptNostr);
console.log(window.decryptNostr);
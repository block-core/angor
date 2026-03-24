
// Importing necessary modules
import { Base64 } from './lib/js-base64/base64.mjs';

const enc = new TextEncoder();
const dec = new TextDecoder();


   
    function getPasswordKey(password) {
        return window.crypto.subtle.importKey("raw", enc.encode(password), "PBKDF2", false, ["deriveKey"]);
    }

    function deriveKey(passwordKey, salt, keyUsage) {
        return window.crypto.subtle.deriveKey(
            {
                name: "PBKDF2",
                salt,
                iterations: 250000,
                hash: "SHA-256",
            },
            passwordKey,
            { name: "AES-GCM", length: 256 },
            false,
            keyUsage
        );
    }

    async function encryptData(secretData, password) {
        try {
            const salt = window.crypto.getRandomValues(new Uint8Array(16));
            const iv = window.crypto.getRandomValues(new Uint8Array(12));
            const passwordKey = await getPasswordKey(password);
            const aesKey = await deriveKey(passwordKey, salt, ["encrypt"]);
            const encryptedContent = await window.crypto.subtle.encrypt(
                {
                    name: "AES-GCM",
                    iv
                },
                aesKey,
                enc.encode(secretData)
            );

            const encryptedContentArr = new Uint8Array(encryptedContent);
            let buff = new Uint8Array(salt.byteLength + iv.byteLength + encryptedContentArr.byteLength);
            buff.set(salt, 0);
            buff.set(iv, salt.byteLength);
            buff.set(encryptedContentArr, salt.byteLength + iv.byteLength);

            return Base64.fromUint8Array(buff);
        } catch (e) {
            console.error(e);
            return "";
        }
    }

    async function  decryptData(encryptedData, password) {
        try {
            const encryptedDataBuff = Base64.toUint8Array(encryptedData);
            const salt = encryptedDataBuff.slice(0, 16);
            const iv = encryptedDataBuff.slice(16, 16 + 12);
            const data = encryptedDataBuff.slice(16 + 12);
            const passwordKey = await getPasswordKey(password);
            const aesKey = await deriveKey(passwordKey, salt, ["decrypt"]);
            const decryptedContent = await window.crypto.subtle.decrypt(
                {
                    name: "AES-GCM",
                    iv
                },
                aesKey,
                data
            );
            return dec.decode(decryptedContent);
        } catch (e) {
            console.error(e);
            return "";
        }
    }



window.encryptData = encryptData;
window.decryptData = decryptData;
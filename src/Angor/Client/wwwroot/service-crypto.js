// Importing necessary modules
import { BehaviorSubject, delay, Observable, of } from "rxjs";
import { Base64 } from 'js-base64';
import * as bip39 from '@scure/bip39';

const enc = new TextEncoder();
const dec = new TextDecoder();

export class CryptoService {
    constructor() {
       
    }
   
    getPasswordKey(password) {
        return window.crypto.subtle.importKey("raw", enc.encode(password), "PBKDF2", false, ["deriveKey"]);
    }

    deriveKey(passwordKey, salt, keyUsage) {
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

    async encryptData(secretData, password) {
        try {
            const salt = window.crypto.getRandomValues(new Uint8Array(16));
            const iv = window.crypto.getRandomValues(new Uint8Array(12));
            const passwordKey = await this.getPasswordKey(password);
            const aesKey = await this.deriveKey(passwordKey, salt, ["encrypt"]);
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

    async decryptData(encryptedData, password) {
        try {
            const encryptedDataBuff = Base64.toUint8Array(encryptedData);
            const salt = encryptedDataBuff.slice(0, 16);
            const iv = encryptedDataBuff.slice(16, 16 + 12);
            const data = encryptedDataBuff.slice(16 + 12);
            const passwordKey = await this.getPasswordKey(password);
            const aesKey = await this.deriveKey(passwordKey, salt, ["decrypt"]);
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
}

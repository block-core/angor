
console.log('NostrTools availability:', {
    mainObject: !!NostrTools,
    nip44: !!NostrTools?.nip44,
    encrypt: !!NostrTools?.nip44?.encrypt
});


async function encryptNostr(nsec, npub, message) {
    try {
        const nonce = SecureRandom.getRandomValues(new Uint8Array(32));
        let sharedSecretHex = await NostrTools.nip44.getConversationKey(nsec, npub);
        return NostrTools.nip44Encrypt(message, sharedSecretHex, nonce);
    } catch (e) {
        console.log(e)
        throw e;
    }
}


async function decryptNostr(nsec, npub, encryptedMessage) {
    try {
        let sharedSecretHex = await NostrTools.nip44.getConversationKey(nsec, npub);
        return NostrTools.nip44.decrypt(encryptedMessage, sharedSecretHex);
    }
    catch (e)
    {
        console.log(e)
        throw e;
    }
}

window.encryptNostr = encryptNostr;
window.decryptNostr = decryptNostr;
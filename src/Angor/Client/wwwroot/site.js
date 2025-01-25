function saveAsFile(fileName, byteBase64) {
    var link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:application/octet-stream;base64,' + byteBase64;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

async function decryptNostrContent(nsec, npub, encryptedContent) {
    try {
        // Ensure the decryption logic is correct and matches the encryption logic
        const decryptedContent = await someDecryptionFunction(nsec, npub, encryptedContent);
        return decryptedContent;
    } catch (error) {
        console.error('Decryption failed', error);
        throw error;
    }
}

// Implement the decryption function
async function someDecryptionFunction(nsec, npub, encryptedContent) {
    try {
        // Sanitize the encrypted content to remove any invalid characters
        const sanitizedContent = encryptedContent.replace(/[^A-Za-z0-9+/=]/g, '');
        // Convert the encrypted content to an ArrayBuffer
        const encryptedBytes = Uint8Array.from(atob(sanitizedContent), c => c.charCodeAt(0));
        const decoder = new TextDecoder();
        const decryptedContent = decoder.decode(encryptedBytes.buffer); // Replace with actual decryption logic
        return decryptedContent;
    } catch (error) {
        console.error('Failed to decode encrypted content', error);
        throw new Error('Invalid encrypted content');
    }
}
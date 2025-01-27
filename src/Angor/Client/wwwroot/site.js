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
        // Split content and IV
        const [content, params] = encryptedContent.split('?');
        
        // Parse IV from params
        const ivParam = new URLSearchParams(params).get('iv');
        if (!ivParam) {
            throw new Error("Missing IV parameter");
        }

        // Base64 decode content and IV separately 
        const decodedContent = atob(content);
        const iv = atob(ivParam);

        // Continue with existing decryption logic...
        return someDecryptionFunction(nsec, npub, decodedContent, iv);
    }
    catch (err) {
        console.error("Failed to decode encrypted content", err);
        throw new Error("Invalid encrypted content");
    }
}

// Implement the decryption function
async function someDecryptionFunction(nsec, npub, decodedContent, iv) {
    try {
        // Sanitize the encrypted content to remove any invalid characters
        const sanitizedContent = decodedContent.replace(/[^A-Za-z0-9+/=]/g, '');
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
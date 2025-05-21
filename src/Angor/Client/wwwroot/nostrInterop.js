
window.nip57Encrypt = async (senderNsec, recipientNpub, content) => {
    const recHex = window.nostrTools.nip19.decode("npub" + recipientNpub).data;
    return window.nostrTools.nip57.encrypt(senderNsec, recHex, content);
  };
  window.nip57Decrypt = async (recipientNsec, senderNpub, encryptedContent) => {
    const pubHex = window.nostrTools.nip19.decode("npub" + senderNpub).data;
    return window.nostrTools.nip57.decrypt(recipientNsec, pubHex, encryptedContent);
  };
  
  window.nip17Delegate = (delegatorNsec, conditions) => {
    return window.nostrTools.nip17.createDelegation(delegatorNsec, conditions);
  };
  
  window.nip17Encrypt = window.nip17Delegate;
  window.nip17Decrypt = async () => {
    throw new Error("NIP‑17 is not decryptable");
  };
  
  window.nip59Encrypt = async (senderNsec, recipientNpub, content, tags = []) => {
    const recHex = window.nostrTools.nip19.decode("npub" + recipientNpub).data;
    return window.nostrTools.nip59.encrypt({
      sk: senderNsec,
      pk: recHex,
      content,
      tags
    });
  };
  window.nip59Decrypt = async (recipientNsec, senderNpub, encryptedEvent) => {
    const pubHex = window.nostrTools.nip19.decode("npub" + senderNpub).data;
    return window.nostrTools.nip59.decrypt({
      sk: recipientNsec,
      pk: pubHex,
      event: encryptedEvent
    });
  };
  
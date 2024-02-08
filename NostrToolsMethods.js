import {nip04} from 'https://cdn.jsdelivr.net/npm/nostr-tools@1.17.0/+esm'

export function encryptNostr(sk1,pk2, message){
    //debugger;
    console.log("encrypting the nostr message to pub key " + pk2)
    return  nip04.encrypt(sk1,pk2, message)
}

export function decryptNostr(sk2,pk1, message){
    //debugger;
    console.log("decrypting the nostr message from pub key " + pk1)
    return  nip04.decrypt(sk2,pk1,message);
}
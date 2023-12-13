// Browser Detection
export async function hasUniSatWallet() {
    return (typeof window.unisat !== 'undefined');
}

// Connecting to UniSat Wallet
export async function connectUniSatWallet() {
    try {
        let accounts = await window.unisat.requestAccounts();
        console.log('connect success', accounts);
        return JSON.stringify(accounts);
    } catch (e) {
        console.log('connect failed');
    }
}

// Get Accounts
export async function getUniSatAccounts() {
    try {
        let res = await window.unisat.getAccounts();
        console.log(res);
        return JSON.stringify(res);

    } catch (e) {
        console.log(e);
    }
}

// Get Network
export async function getUniSatNetwork() {
    try {
        let res = await window.unisat.getNetwork();
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Switch Network
export async function switchUniSatNetwork(network) {
    try {
        let res = await window.unisat.switchNetwork(network);
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Get Public Key
export async function getUniSatPublicKey() {
    try {
        let res = await window.unisat.getPublicKey();
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Get Balance
export async function getUniSatBalance() {
    try {
        let res = await window.unisat.getBalance();
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Get Inscriptions
export async function getUniSatInscriptions(cursor, size) {
    try {
        let res = await window.unisat.getInscriptions(cursor, size);
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Send Bitcoin
export async function sendBitcoinUniSat(toAddress, satoshis, options) {
    try {
        let txid = await window.unisat.sendBitcoin(toAddress, satoshis, options);
        console.log(txid);
        return JSON.stringify(txid);
    } catch (e) {
        console.log(e);
    }
}

// Send Inscription
export async function sendInscriptionUniSat(address, inscriptionId, options) {
    try {
        let { txid } = await window.unisat.sendInscription(address, inscriptionId, options);
        console.log("send Inscription", txid);
        return JSON.stringify(txid);
    } catch (e) {
        console.log(e);
    }
}

// Sign Message
export async function signMessageUniSat(msg, type) {
    try {
        let res = await window.unisat.signMessage(msg, type);
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Push Transaction
export async function pushTransactionUniSat(options) {
    try {
        let txid = await window.unisat.pushTx(options);
        console.log(txid);
        return JSON.stringify(txid);
    } catch (e) {
        console.log(e);
    }
}

// Sign PSBT
export async function signPsbtUniSat(psbtHex, options) {
    try {
        let res = await window.unisat.signPsbt(psbtHex, options);
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Sign Multiple PSBTs
export async function signPsbtsUniSat(psbtHexs, options) {
    try {
        let res = await window.unisat.signPsbts(psbtHexs, options);
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

// Push PSBT
export async function pushPsbtUniSat(psbtHex) {
    try {
        let res = await window.unisat.pushPsbt(psbtHex);
        console.log(res);
        return JSON.stringify(res);
    } catch (e) {
        console.log(e);
    }
}

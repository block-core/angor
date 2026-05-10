#!/usr/bin/env node

// Decrypt an encrypted Angor log blob from a Blossom URL.
//
// Usage:
//   node decrypt-log.mjs <nsecOrHex> <senderPubkeyHex> <blobUrl> [output.zip]
//
// Arguments:
//   nsecOrHex        Your support nsec (nsec1... or 64-char hex)
//   senderPubkeyHex  Sender's pubkey from the DM (64-char hex)
//   blobUrl          Blossom URL of the encrypted blob
//   output.zip       Output file path (default: decrypted-log.zip)

import { nip04, nip19 } from 'nostr-tools';
import { writeFileSync } from 'fs';

const args = process.argv.slice(2);

if (args.length < 3) {
  console.log('Usage: node decrypt-log.mjs <nsecOrHex> <senderPubkeyHex> <blobUrl> [output.zip]');
  console.log();
  console.log('  nsecOrHex        Support nsec (nsec1... or 64-char hex private key)');
  console.log('  senderPubkeyHex  Sender pubkey from the DM (64 hex chars)');
  console.log('  blobUrl          Blossom URL of the encrypted blob');
  console.log('  output.zip       Output file (default: decrypted-log.zip)');
  process.exit(1);
}

const [nsecInput, senderPubkey, blobUrl] = args;
const outputPath = args[3] || 'decrypted-log.zip';

// Parse nsec - accept both bech32 (nsec1...) and hex
let privateKeyHex;
if (nsecInput.startsWith('nsec1')) {
  const decoded = nip19.decode(nsecInput);
  if (decoded.type !== 'nsec') {
    console.error('ERROR: Invalid nsec');
    process.exit(1);
  }
  // decoded.data is a Uint8Array, convert to hex
  privateKeyHex = Buffer.from(decoded.data).toString('hex');
} else if (/^[0-9a-f]{64}$/i.test(nsecInput)) {
  privateKeyHex = nsecInput.toLowerCase();
} else {
  console.error('ERROR: nsec must be bech32 (nsec1...) or 64-char hex');
  process.exit(1);
}

// Validate sender pubkey
if (!/^[0-9a-f]{64}$/i.test(senderPubkey)) {
  console.error('ERROR: senderPubkeyHex must be 64 hex characters');
  process.exit(1);
}

// Validate URL
if (!blobUrl.startsWith('https://')) {
  console.error('ERROR: blobUrl must be HTTPS');
  process.exit(1);
}

console.log(`Downloading blob from ${blobUrl}...`);

const response = await fetch(blobUrl);
if (!response.ok) {
  console.error(`ERROR: Download failed: ${response.status} ${response.statusText}`);
  process.exit(1);
}

const blobContent = await response.text();
console.log(`Downloaded ${blobContent.length} chars`);

// Validate NIP-04 format: base64?iv=base64
if (!blobContent.includes('?iv=')) {
  console.error('ERROR: Blob is not in NIP-04 format (expected base64?iv=base64)');
  process.exit(1);
}

console.log('Decrypting...');

try {
  const zipBase64 = await nip04.decrypt(privateKeyHex, senderPubkey, blobContent);
  const zipBytes = Buffer.from(zipBase64, 'base64');

  // Basic sanity check - zip files start with PK (0x50 0x4B)
  if (zipBytes.length < 4 || zipBytes[0] !== 0x50 || zipBytes[1] !== 0x4B) {
    console.error('WARNING: Decrypted content does not look like a zip file');
    console.error('First bytes:', zipBytes.slice(0, 8).toString('hex'));
  }

  writeFileSync(outputPath, zipBytes);
  console.log(`Decrypted zip saved to: ${outputPath} (${zipBytes.length} bytes)`);
} catch (err) {
  console.error(`ERROR: Decryption failed: ${err.message}`);
  process.exit(1);
}

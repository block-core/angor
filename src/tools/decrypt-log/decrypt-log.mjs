#!/usr/bin/env node

// Decrypt an encrypted Angor log blob.
//
// Mode 1 - From event ID (fetches DM from relay, extracts sender + URL):
//   node decrypt-log.mjs --event <nsec> <eventId> [output.zip]
//
// Mode 2 - Manual (provide all details):
//   node decrypt-log.mjs <nsec> <senderPubkeyHex> <blobUrl> [output.zip]

import { nip04, nip44, nip19, getPublicKey } from 'nostr-tools';
import { Relay } from 'nostr-tools/relay';
import { writeFileSync } from 'fs';
import 'websocket-polyfill';

// Decrypt content, auto-detecting NIP-04 vs NIP-44 format
async function decryptContent(privateKeyHex, pubkey, content) {
  if (content.includes('?iv=')) {
    // NIP-04 format: base64?iv=base64
    return await nip04.decrypt(privateKeyHex, pubkey, content);
  }
  // NIP-44 format: plain base64
  const privKeyBytes = Buffer.from(privateKeyHex, 'hex');
  const conversationKey = nip44.v2.utils.getConversationKey(privKeyBytes, pubkey);
  return nip44.v2.decrypt(content, conversationKey);
}

const DEFAULT_RELAYS = [
  'wss://relay.angor.io',
  'wss://relay2.angor.io',
  'wss://relay.damus.io',
  'wss://nos.lol',
];

const args = process.argv.slice(2);

// Parse nsec helper
function parseNsec(input) {
  if (input.startsWith('nsec1')) {
    const decoded = nip19.decode(input);
    if (decoded.type !== 'nsec') throw new Error('Invalid nsec');
    return Buffer.from(decoded.data).toString('hex');
  } else if (/^[0-9a-f]{64}$/i.test(input)) {
    return input.toLowerCase();
  }
  throw new Error('nsec must be bech32 (nsec1...) or 64-char hex');
}

// Fetch event by ID from relays
async function fetchEvent(eventId, privateKeyHex) {
  const pubkey = getPublicKey(Buffer.from(privateKeyHex, 'hex'));

  for (const url of DEFAULT_RELAYS) {
    try {
      console.log(`Trying relay ${url}...`);
      const relay = await Relay.connect(url);

      const event = await new Promise((resolve, reject) => {
        const timeout = setTimeout(() => { relay.close(); reject(new Error('timeout')); }, 10000);
        const sub = relay.subscribe(
          [{ ids: [eventId], kinds: [4] }],
          {
            onevent(ev) { clearTimeout(timeout); relay.close(); resolve(ev); },
            oneose() { clearTimeout(timeout); relay.close(); reject(new Error('not found')); },
          }
        );
      });

      return event;
    } catch (err) {
      console.log(`  ${err.message}`);
    }
  }
  return null;
}

// Main
if (args[0] === '--event') {
  // Mode 1: event ID mode
  if (args.length < 3) {
    console.log('Usage: node decrypt-log.mjs --event <nsec> <eventId> [output.zip]');
    process.exit(1);
  }

  const privateKeyHex = parseNsec(args[1]);
  let eventId = args[2];
  const outputPath = args[3] || 'decrypted-log.zip';

  // Handle note1... bech32 event IDs
  if (eventId.startsWith('note1')) {
    const decoded = nip19.decode(eventId);
    eventId = decoded.data;
  }

  console.log(`Fetching event ${eventId.slice(0, 12)}...`);
  const event = await fetchEvent(eventId, privateKeyHex);
  if (!event) {
    console.error('ERROR: Could not find event on any relay');
    process.exit(1);
  }

  const senderPubkey = event.pubkey;
  console.log(`Sender: ${senderPubkey}`);

  // Decrypt the DM to get the message with blob URL (NIP-44 or NIP-04)
  const dmContent = await decryptContent(privateKeyHex, senderPubkey, event.content);
  console.log('DM content:');
  console.log(dmContent);
  console.log();

  // Extract blob URL from DM
  const urlMatch = dmContent.match(/URL:\s*(https?:\/\/\S+)/);
  if (!urlMatch) {
    console.error('ERROR: No URL found in DM content');
    process.exit(1);
  }

  const blobUrl = urlMatch[1];
  await downloadAndDecrypt(privateKeyHex, senderPubkey, blobUrl, outputPath);

} else {
  // Mode 2: manual mode
  if (args.length < 3) {
    console.log('Usage:');
    console.log('  node decrypt-log.mjs --event <nsec> <eventId> [output.zip]');
    console.log('  node decrypt-log.mjs <nsec> <senderPubkeyHex> <blobUrl> [output.zip]');
    process.exit(1);
  }

  const privateKeyHex = parseNsec(args[0]);
  const senderPubkey = args[1];
  const blobUrl = args[2];
  const outputPath = args[3] || 'decrypted-log.zip';

  if (!/^[0-9a-f]{64}$/i.test(senderPubkey)) {
    console.error('ERROR: senderPubkeyHex must be 64 hex characters');
    process.exit(1);
  }

  await downloadAndDecrypt(privateKeyHex, senderPubkey, blobUrl, outputPath);
}

async function downloadAndDecrypt(privateKeyHex, senderPubkey, blobUrl, outputPath) {
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

  console.log('Decrypting blob...');

  try {
    let zipBase64;

    if (blobContent.includes('?iv=')) {
      // Legacy NIP-04 single-chunk format
      zipBase64 = await nip04.decrypt(privateKeyHex, senderPubkey, blobContent);
    } else {
      // NIP-44 v2 — may be multi-chunk (newline-separated)
      const privKeyBytes = Buffer.from(privateKeyHex, 'hex');
      const conversationKey = nip44.v2.utils.getConversationKey(privKeyBytes, senderPubkey);
      const chunks = blobContent.trim().split('\n');
      console.log(`Decrypting ${chunks.length} chunk(s)...`);
      const decryptedChunks = chunks.map(chunk => nip44.v2.decrypt(chunk.trim(), conversationKey));
      zipBase64 = decryptedChunks.join('');
    }

    const zipBytes = Buffer.from(zipBase64, 'base64');

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
}

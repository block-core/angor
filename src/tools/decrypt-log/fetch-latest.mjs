#!/usr/bin/env node

// Fetch and decrypt the latest Angor log export.
//
// Usage:
//   node fetch-latest.mjs [--event <eventId>] [output.zip]
//
// Config: .env file in the same directory (gitignored):
//   SUPPORT_NSEC=nsec1...
//   OUTPUT_DIR=C:\Users\me\Downloads     (optional)

import { nip04, nip44, nip19, getPublicKey } from 'nostr-tools';
import { SimplePool } from 'nostr-tools/pool';
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { fileURLToPath } from 'url';
import { join, dirname } from 'path';
import { homedir } from 'os';
import 'websocket-polyfill';

const DEFAULT_RELAYS = [
  'wss://relay.angor.io',
  'wss://relay2.angor.io',
  'wss://relay.damus.io',
  'wss://nos.lol',
];

const scriptDir = dirname(fileURLToPath(import.meta.url));
const envPath = join(scriptDir, '.env');

// Minimal .env parser
const config = {};
if (existsSync(envPath)) {
  for (const line of readFileSync(envPath, 'utf8').split('\n')) {
    const m = line.match(/^\s*([A-Z_]+)\s*=\s*(.+?)\s*$/);
    if (m && !line.trim().startsWith('#')) config[m[1]] = m[2];
  }
}

const nsec = process.env.SUPPORT_NSEC || config.SUPPORT_NSEC;
if (!nsec || nsec.includes('PASTE')) {
  console.error(`ERROR: SUPPORT_NSEC not set. Create ${envPath} containing:`);
  console.error('SUPPORT_NSEC=nsec1...');
  process.exit(1);
}

const privateKeyHex = nsec.startsWith('nsec1')
  ? Buffer.from(nip19.decode(nsec).data).toString('hex')
  : nsec.toLowerCase();
const supportPubkey = getPublicKey(Buffer.from(privateKeyHex, 'hex'));

// --- Args ---
const args = process.argv.slice(2);
let eventIdArg = null;
let outputArg = null;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--event') eventIdArg = args[++i];
  else outputArg = args[i];
}

// --- Decrypt helpers (NIP-44 / NIP-04 auto-detect) ---
async function decryptContent(pubkey, content) {
  if (content.includes('?iv=')) return await nip04.decrypt(privateKeyHex, pubkey, content);
  const conversationKey = nip44.v2.utils.getConversationKey(Buffer.from(privateKeyHex, 'hex'), pubkey);
  return nip44.v2.decrypt(content, conversationKey);
}

// --- Fetch event ---
const pool = new SimplePool();
let event;

if (eventIdArg) {
  let id = eventIdArg.startsWith('note1') ? nip19.decode(eventIdArg).data : eventIdArg;
  console.log(`Fetching event ${id.slice(0, 12)}...`);
  event = await pool.get(DEFAULT_RELAYS, { ids: [id], kinds: [4] });
} else {
  console.log('Fetching latest log-export DM...');
  const events = await pool.querySync(DEFAULT_RELAYS, { kinds: [4], '#p': [supportPubkey], limit: 5 });
  const sorted = [...new Map(events.map(e => [e.id, e])).values()].sort((a, b) => b.created_at - a.created_at);
  event = sorted[0];
}

if (!event) {
  console.error('ERROR: no DM found');
  pool.close(DEFAULT_RELAYS);
  process.exit(1);
}

console.log(`Event:  ${event.id}`);
console.log(`Sender: ${nip19.npubEncode(event.pubkey)}`);
console.log(`Date:   ${new Date(event.created_at * 1000).toISOString()}`);

const dmContent = await decryptContent(event.pubkey, event.content);
console.log('\n' + dmContent + '\n');

const urlMatch = dmContent.match(/URL:\s*(https?:\/\/\S+)/);
if (!urlMatch) {
  console.error('ERROR: DM contains no blob URL (not a log export?)');
  pool.close(DEFAULT_RELAYS);
  process.exit(1);
}

// --- Download + decrypt blob ---
const blobUrl = urlMatch[1];
console.log(`Downloading ${blobUrl}...`);
const res = await fetch(blobUrl);
if (!res.ok) {
  console.error(`ERROR: download failed: ${res.status} ${res.statusText}`);
  pool.close(DEFAULT_RELAYS);
  process.exit(1);
}
const blobContent = await res.text();

let zipBase64;
if (blobContent.includes('?iv=')) {
  zipBase64 = await nip04.decrypt(privateKeyHex, event.pubkey, blobContent);
} else {
  const conversationKey = nip44.v2.utils.getConversationKey(Buffer.from(privateKeyHex, 'hex'), event.pubkey);
  const chunks = blobContent.trim().split('\n');
  console.log(`Decrypting ${chunks.length} chunk(s)...`);
  zipBase64 = chunks.map(c => nip44.v2.decrypt(c.trim(), conversationKey)).join('');
}
const zipBytes = Buffer.from(zipBase64, 'base64');

const stamp = new Date(event.created_at * 1000).toISOString().replace(/[:T]/g, '-').slice(0, 19);
const outDir = config.OUTPUT_DIR || join(homedir(), 'Downloads');
const outputPath = outputArg || join(outDir, `angor-log-${stamp}.zip`);
writeFileSync(outputPath, zipBytes);
console.log(`Saved: ${outputPath} (${zipBytes.length} bytes)`);

pool.close(DEFAULT_RELAYS);
process.exit(0);

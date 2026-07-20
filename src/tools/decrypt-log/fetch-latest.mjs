#!/usr/bin/env node

// Fetch and decrypt the latest Angor log export, with optional Discord upload.
//
// Usage:
//   node fetch-latest.mjs [--event <eventId>] [--no-upload] [output.zip]
//
// Config file (same directory, gitignored): fetch-config.json
//   {
//     "nsec": "nsec1...",                        // support nsec (required)
//     "discordWebhookUrl": "https://discord...", // optional: enables upload prompt
//     "outputDir": "C:\\Users\\me\\Downloads"    // optional: default save location
//   }

import { nip04, nip44, nip19, getPublicKey } from 'nostr-tools';
import { SimplePool } from 'nostr-tools/pool';
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { fileURLToPath } from 'url';
import { join, dirname } from 'path';
import { homedir } from 'os';
import { createInterface } from 'readline/promises';
import 'websocket-polyfill';

const DEFAULT_RELAYS = [
  'wss://relay.angor.io',
  'wss://relay2.angor.io',
  'wss://relay.damus.io',
  'wss://nos.lol',
];

const scriptDir = dirname(fileURLToPath(import.meta.url));
const configPath = join(scriptDir, 'fetch-config.json');

if (!existsSync(configPath)) {
  console.error(`ERROR: config file not found: ${configPath}`);
  console.error('Create it with: { "nsec": "nsec1...", "discordWebhookUrl": "https://discord.com/api/webhooks/..." }');
  process.exit(1);
}

const config = JSON.parse(readFileSync(configPath, 'utf8'));
if (!config.nsec) {
  console.error('ERROR: "nsec" missing from fetch-config.json');
  process.exit(1);
}

const privateKeyHex = config.nsec.startsWith('nsec1')
  ? Buffer.from(nip19.decode(config.nsec).data).toString('hex')
  : config.nsec.toLowerCase();
const supportPubkey = getPublicKey(Buffer.from(privateKeyHex, 'hex'));

// --- Args ---
const args = process.argv.slice(2);
let eventIdArg = null;
let noUpload = false;
let outputArg = null;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--event') eventIdArg = args[++i];
  else if (args[i] === '--no-upload') noUpload = true;
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
const outDir = config.outputDir || join(homedir(), 'Downloads');
const outputPath = outputArg || join(outDir, `angor-log-${stamp}.zip`);
writeFileSync(outputPath, zipBytes);
console.log(`Saved: ${outputPath} (${zipBytes.length} bytes)`);

// --- Optional Discord upload ---
if (config.discordWebhookUrl && !noUpload) {
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  const answer = await rl.question('Upload decrypted zip to Discord channel? [y/N] ');
  rl.close();

  if (answer.trim().toLowerCase() === 'y') {
    if (zipBytes.length > 10 * 1024 * 1024) {
      console.error('ERROR: zip exceeds Discord 10MB webhook limit; not uploading');
    } else {
      const meta = dmContent.split('\n').slice(1).join('\n');
      const form = new FormData();
      form.append('payload_json', JSON.stringify({
        content: `📦 Decrypted log export\n${meta}`,
      }));
      form.append('files[0]', new Blob([zipBytes], { type: 'application/zip' }), `angor-log-${stamp}.zip`);
      const up = await fetch(config.discordWebhookUrl, { method: 'POST', body: form });
      if (up.ok) console.log('Uploaded to Discord.');
      else console.error(`ERROR: Discord upload failed: ${up.status} ${await up.text().catch(() => '')}`);
    }
  }
}

pool.close(DEFAULT_RELAYS);
process.exit(0);

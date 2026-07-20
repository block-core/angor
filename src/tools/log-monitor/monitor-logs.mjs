#!/usr/bin/env node

// Monitor Nostr relays for Angor log-export DMs and post to Discord.
//
// Watches for kind-4 DMs addressed to the Angor support npub. When a new
// log export arrives, posts a notification to a Discord webhook.
//
// If the support nsec is provided, the DM content is decrypted and the
// notification includes the metadata (blob URL, app version, platform,
// network, timestamp). Otherwise a notify-only message is posted with
// just the sender pubkey and event id.
//
// Usage:
//   node monitor-logs.mjs
//
// Configuration (env vars):
//   DISCORD_WEBHOOK_URL   (required)  Discord webhook(s) to post to;
//                                     comma-separated for multiple channels
//                                     (e.g. main Discord + Armada Angor channel)
//   SUPPORT_NPUB          (required*) support npub (npub1... or 64-char hex)
//   SUPPORT_NSEC          (optional)  support nsec (nsec1... or 64-char hex);
//                                     enables decryption. *If set, SUPPORT_NPUB
//                                     is derived from it and may be omitted.
//   RELAYS                (optional)  comma-separated relay URLs
//   LOOKBACK_HOURS        (optional)  how far back to scan on startup (default 1)
//   STATE_FILE            (optional)  path to seen-events state file
//                                     (default ./seen-events.json)

import { nip04, nip44, nip19, getPublicKey } from 'nostr-tools';
import { SimplePool } from 'nostr-tools/pool';
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { fileURLToPath } from 'url';
import 'websocket-polyfill';

const DEFAULT_RELAYS = [
  'wss://relay.angor.io',
  'wss://relay2.angor.io',
  'wss://relay.damus.io',
  'wss://nos.lol',
];

// --- Config ---------------------------------------------------------------

function parseKey(input, prefix) {
  if (!input) return null;
  input = input.trim();
  if (input.startsWith(prefix)) {
    const decoded = nip19.decode(input);
    // nsec decodes to Uint8Array, npub decodes to hex string
    return typeof decoded.data === 'string'
      ? decoded.data.toLowerCase()
      : Buffer.from(decoded.data).toString('hex');
  }
  if (/^[0-9a-f]{64}$/i.test(input)) return input.toLowerCase();
  throw new Error(`Key must be bech32 (${prefix}...) or 64-char hex`);
}

const webhookUrls = (process.env.DISCORD_WEBHOOK_URL || '')
  .split(',')
  .map(u => u.trim())
  .filter(Boolean);
if (webhookUrls.length === 0) {
  console.error('ERROR: DISCORD_WEBHOOK_URL is required (comma-separated for multiple)');
  process.exit(1);
}

let supportNsecHex = null;
try {
  supportNsecHex = parseKey(process.env.SUPPORT_NSEC, 'nsec1');
} catch (err) {
  console.error(`ERROR: invalid SUPPORT_NSEC: ${err.message}`);
  process.exit(1);
}

let supportNpubHex;
if (supportNsecHex) {
  supportNpubHex = getPublicKey(Buffer.from(supportNsecHex, 'hex'));
} else {
  try {
    supportNpubHex = parseKey(process.env.SUPPORT_NPUB, 'npub1');
  } catch (err) {
    console.error(`ERROR: invalid SUPPORT_NPUB: ${err.message}`);
    process.exit(1);
  }
  if (!supportNpubHex) {
    console.error('ERROR: SUPPORT_NPUB (or SUPPORT_NSEC) is required');
    process.exit(1);
  }
}

const relays = (process.env.RELAYS || '')
  .split(',')
  .map(r => r.trim())
  .filter(Boolean);
const relayUrls = relays.length > 0 ? relays : DEFAULT_RELAYS;

const lookbackHours = Number(process.env.LOOKBACK_HOURS || 1);
const stateFile = process.env.STATE_FILE || fileURLToPath(new URL('./seen-events.json', import.meta.url));

// --- Seen-event state (dedup across restarts + multi-relay) ----------------

const seen = new Set();
try {
  if (existsSync(stateFile)) {
    for (const id of JSON.parse(readFileSync(stateFile, 'utf8'))) seen.add(id);
  }
} catch (err) {
  console.warn(`WARN: could not read state file: ${err.message}`);
}

function persistSeen() {
  try {
    // keep the most recent 5000 ids
    writeFileSync(stateFile, JSON.stringify([...seen].slice(-5000)));
  } catch (err) {
    console.warn(`WARN: could not write state file: ${err.message}`);
  }
}

// --- Decryption (same auto-detect as decrypt-log.mjs) -----------------------

async function decryptContent(privateKeyHex, pubkey, content) {
  if (content.includes('?iv=')) {
    return await nip04.decrypt(privateKeyHex, pubkey, content);
  }
  const privKeyBytes = Buffer.from(privateKeyHex, 'hex');
  const conversationKey = nip44.v2.utils.getConversationKey(privKeyBytes, pubkey);
  return nip44.v2.decrypt(content, conversationKey);
}

function parseDmMetadata(dmContent) {
  const get = re => dmContent.match(re)?.[1]?.trim() ?? null;
  return {
    isLogExport: /^Log Export/m.test(dmContent),
    url: get(/URL:\s*(https?:\/\/\S+)/),
    version: get(/Version:\s*(.+)/),
    platform: get(/Platform:\s*(.+)/),
    network: get(/Network:\s*(.+)/),
    timestamp: get(/Timestamp:\s*(.+)/),
  };
}

// --- Discord ---------------------------------------------------------------

async function postToDiscord(embed) {
  const errors = [];
  for (const url of webhookUrls) {
    try {
      const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ embeds: [embed] }),
      });
      if (!res.ok) {
        const body = await res.text().catch(() => '');
        errors.push(`webhook ${url.slice(0, 60)}...: ${res.status} ${res.statusText} ${body}`);
      }
    } catch (err) {
      errors.push(`webhook ${url.slice(0, 60)}...: ${err.message}`);
    }
  }
  // Fail (and allow retry) only if ALL webhooks failed
  if (errors.length === webhookUrls.length) {
    throw new Error(`All Discord webhooks failed: ${errors.join('; ')}`);
  }
  for (const e of errors) console.warn(`  WARN: ${e}`);
}

function buildEmbed(event, meta) {
  const npub = nip19.npubEncode(event.pubkey);
  const fields = [
    { name: 'Sender', value: `\`${npub}\``, inline: false },
    { name: 'Event ID', value: `\`${event.id}\``, inline: false },
    { name: 'Received', value: new Date(event.created_at * 1000).toISOString(), inline: true },
  ];

  if (meta) {
    if (meta.version) fields.push({ name: 'Version', value: meta.version, inline: true });
    if (meta.network) fields.push({ name: 'Network', value: meta.network, inline: true });
    if (meta.platform) fields.push({ name: 'Platform', value: meta.platform, inline: false });
    if (meta.url) fields.push({ name: 'Blob URL', value: meta.url, inline: false });
    if (meta.timestamp) fields.push({ name: 'Exported At', value: meta.timestamp, inline: true });
  }

  return {
    title: meta ? '📋 New Angor Log Export' : '📋 New DM to Angor Support (possible log export)',
    color: 0xf7931a,
    fields,
    footer: { text: 'Angor log monitor' },
    timestamp: new Date().toISOString(),
  };
}

// --- Event handling ----------------------------------------------------------

async function handleEvent(event) {
  if (seen.has(event.id)) return;
  seen.add(event.id);
  persistSeen();

  console.log(`[${new Date().toISOString()}] DM ${event.id.slice(0, 12)}... from ${event.pubkey.slice(0, 12)}...`);

  let meta = null;
  if (supportNsecHex) {
    try {
      const dmContent = await decryptContent(supportNsecHex, event.pubkey, event.content);
      meta = parseDmMetadata(dmContent);
      if (!meta.isLogExport) {
        console.log('  Decrypted DM is not a log export; posting generic notification');
        meta = null;
      }
    } catch (err) {
      console.warn(`  Decryption failed (${err.message}); posting notify-only`);
    }
  }

  try {
    await postToDiscord(buildEmbed(event, meta));
    console.log('  Posted to Discord');
  } catch (err) {
    console.error(`  ${err.message}`);
    // allow retry if the same event arrives from another relay
    seen.delete(event.id);
    persistSeen();
  }
}

// --- Main --------------------------------------------------------------------

console.log('Angor log monitor starting');
console.log(`  Support pubkey: ${nip19.npubEncode(supportNpubHex)}`);
console.log(`  Decryption:     ${supportNsecHex ? 'enabled (nsec provided)' : 'disabled (notify-only)'}`);
console.log(`  Relays:         ${relayUrls.join(', ')}`);
console.log(`  Lookback:       ${lookbackHours}h, state file: ${stateFile}`);
console.log(`  Webhooks:       ${webhookUrls.length} Discord channel(s)`);

const pool = new SimplePool();
const since = Math.floor(Date.now() / 1000) - lookbackHours * 3600;

pool.subscribeMany(
  relayUrls,
  { kinds: [4], '#p': [supportNpubHex], since },
  {
    onevent: ev => { handleEvent(ev).catch(err => console.error(`ERROR: ${err.message}`)); },
    oneose: () => console.log('Initial sync complete; watching for new events...'),
  },
);

process.on('SIGINT', () => {
  console.log('\nShutting down');
  pool.close(relayUrls);
  persistSeen();
  process.exit(0);
});

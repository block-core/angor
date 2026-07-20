#!/usr/bin/env node
// List recent kind-4 DMs sent to a pubkey (most recent first): id, sender, created_at
import { nip19 } from 'nostr-tools';
import { SimplePool } from 'nostr-tools/pool';
import 'websocket-polyfill';

const npub = process.argv[2];
const hex = npub.startsWith('npub1') ? nip19.decode(npub).data : npub;
const relays = ['wss://relay.angor.io', 'wss://relay2.angor.io', 'wss://relay.damus.io', 'wss://nos.lol'];
const pool = new SimplePool();
const events = await pool.querySync(relays, { kinds: [4], '#p': [hex], limit: 10 });
const unique = [...new Map(events.map(e => [e.id, e])).values()]
  .sort((a, b) => b.created_at - a.created_at);
for (const e of unique) {
  console.log(`${new Date(e.created_at * 1000).toISOString()}  ${e.id}  from ${e.pubkey}`);
}
pool.close(relays);
process.exit(0);

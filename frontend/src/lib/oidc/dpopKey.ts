/**
 * Global DPoP keypair singleton.
 *
 * Loads persisted keypair from IndexedDB on first call.
 * Generates a new keypair if none exists.
 * All subsequent calls return the same in-memory instance.
 */

import { generateDPoPKeyPair } from './dpop';
import { loadKeyPair, saveKeyPair, clearKeyPair } from './keyStorage';

let _keyPair: CryptoKeyPair | null = null;

export async function getDPoPKeyPair(): Promise<CryptoKeyPair> {
  if (_keyPair) return _keyPair;

  const stored = await loadKeyPair();
  if (stored) {
    _keyPair = stored;
    return _keyPair;
  }

  const fresh = await generateDPoPKeyPair();
  await saveKeyPair(fresh);
  _keyPair = fresh;
  return _keyPair;
}

/** Call on logout to force new keypair next session. */
export async function rotateDPoPKeyPair(): Promise<void> {
  _keyPair = null;
  await clearKeyPair();
}

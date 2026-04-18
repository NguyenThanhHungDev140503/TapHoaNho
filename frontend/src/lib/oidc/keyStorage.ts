/**
 * DPoP keypair persistence via IndexedDB.
 *
 * Why IndexedDB, not localStorage?
 *   - CryptoKey objects are structured-cloneable but NOT JSON-serialisable.
 *   - localStorage only stores strings.
 *   - IndexedDB stores arbitrary structured-clone data → native CryptoKey
 *     storage with no need to export the private key bytes.
 *   - The private key remains non-extractable (extractable=false) at all times.
 */

const DB_NAME = 'dpop-key-store';
const DB_VERSION = 1;
const STORE_NAME = 'keypairs';
const KEY_ID = 'dpop-keypair';

function openDB(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);

    req.onupgradeneeded = (event) => {
      const db = (event.target as IDBOpenDBRequest).result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME);
      }
    };

    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

export async function loadKeyPair(): Promise<CryptoKeyPair | null> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, 'readonly');
    const req = tx.objectStore(STORE_NAME).get(KEY_ID);
    req.onsuccess = () => resolve((req.result as CryptoKeyPair) ?? null);
    req.onerror = () => reject(req.error);
  });
}

export async function saveKeyPair(keyPair: CryptoKeyPair): Promise<void> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, 'readwrite');
    const req = tx.objectStore(STORE_NAME).put(keyPair, KEY_ID);
    req.onsuccess = () => resolve();
    req.onerror = () => reject(req.error);
  });
}

export async function clearKeyPair(): Promise<void> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, 'readwrite');
    const req = tx.objectStore(STORE_NAME).delete(KEY_ID);
    req.onsuccess = () => resolve();
    req.onerror = () => reject(req.error);
  });
}

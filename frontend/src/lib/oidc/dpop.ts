/**
 * DPoP (Demonstrating Proof-of-Possession) — RFC 9449
 *
 * Flow:
 *   1. On first load, generate an ECDSA P-384 CryptoKeyPair via SubtleCrypto.
 *      The private key is non-extractable — it never leaves the browser.
 *   2. Persist the keypair to IndexedDB so it survives page reloads.
 *   3. Before every request, build a signed DPoP proof JWT:
 *        { typ:"dpop+jwt", alg:"ES384", jwk:<publicKeyJwk> }
 *        { jti:<uuid>, htm:<METHOD>, htu:<url-no-query>, iat:<now>,
 *          ath:<base64url-sha256(access_token)> }
 *   4. Attach to request:
 *        Authorization: DPoP <access_token>
 *        DPoP: <proof_jwt>
 *
 * Why ES384 instead of ES256?
 *   RFC 9449 recommends ES384 (P-384) for long-lived DPoP keys because
 *   P-384 provides 192-bit security level — well above NIST's post-2030
 *   requirement of ≥ 128 bits while remaining efficiently computable
 *   in browser WebCrypto.
 */

const ALG = 'ES384';
const CURVE = 'P-384';

// ─────────────────────────────────────────────────────────────────────────────
// Key generation
// ─────────────────────────────────────────────────────────────────────────────

export async function generateDPoPKeyPair(): Promise<CryptoKeyPair> {
  return crypto.subtle.generateKey(
    {
      name: 'ECDSA',
      namedCurve: CURVE,
    },
    false,          // non-extractable: private key cannot leave the browser
    ['sign', 'verify'],
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// JWK thumbprint (RFC 7638)
// Used to build cnf.jkt — the binding claim that ties a token to a key.
// ─────────────────────────────────────────────────────────────────────────────

export async function computeJwkThumbprint(publicKey: CryptoKey): Promise<string> {
  const jwk = await crypto.subtle.exportKey('jwk', publicKey);
  // RFC 7638 § 3.2: sorted, minimal set of required members
  const canonical = JSON.stringify({
    crv: jwk.crv,
    kty: jwk.kty,
    x: jwk.x,
    y: jwk.y,
  });
  const hash = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(canonical));
  return base64urlEncode(new Uint8Array(hash));
}

// ─────────────────────────────────────────────────────────────────────────────
// DPoP proof JWT
// ─────────────────────────────────────────────────────────────────────────────

export interface DPoPProofOptions {
  /** HTTP method, e.g. "GET" */
  htm: string;
  /** Full request URL (query string stripped per RFC 9449 § 4.2) */
  htu: string;
  /** Access token — when present, ath = BASE64URL(SHA-256(ascii(token))) */
  accessToken?: string;
  /** Server-issued nonce from DPoP-Nonce response header */
  nonce?: string;
}

export async function buildDPoPProof(
  keyPair: CryptoKeyPair,
  opts: DPoPProofOptions,
): Promise<string> {
  const publicJwk = await crypto.subtle.exportKey('jwk', keyPair.publicKey);

  // Strip query string and fragment per RFC 9449 § 4.2
  const htu = stripQueryAndFragment(opts.htu);

  const header = {
    typ: 'dpop+jwt',
    alg: ALG,
    jwk: {
      kty: publicJwk.kty,
      crv: publicJwk.crv,
      x: publicJwk.x,
      y: publicJwk.y,
    },
  };

  const payload: Record<string, unknown> = {
    jti: crypto.randomUUID(),
    htm: opts.htm.toUpperCase(),
    htu,
    iat: Math.floor(Date.now() / 1000),
  };

  if (opts.nonce) {
    payload['nonce'] = opts.nonce;
  }

  if (opts.accessToken) {
    payload['ath'] = await accessTokenHash(opts.accessToken);
  }

  const headerB64 = base64urlEncode(new TextEncoder().encode(JSON.stringify(header)));
  const payloadB64 = base64urlEncode(new TextEncoder().encode(JSON.stringify(payload)));
  const signingInput = `${headerB64}.${payloadB64}`;

  const signature = await crypto.subtle.sign(
    { name: 'ECDSA', hash: { name: 'SHA-384' } },
    keyPair.privateKey,
    new TextEncoder().encode(signingInput),
  );

  return `${signingInput}.${base64urlEncode(new Uint8Array(signature))}`;
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

function base64urlEncode(data: Uint8Array): string {
  let str = '';
  for (const byte of data) str += String.fromCharCode(byte);
  return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
}

async function accessTokenHash(token: string): Promise<string> {
  const hash = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(token),
  );
  return base64urlEncode(new Uint8Array(hash));
}

function stripQueryAndFragment(url: string): string {
  try {
    const u = new URL(url);
    u.search = '';
    u.hash = '';
    return u.toString();
  } catch {
    // Not a full URL (relative path) — strip manually
    return url.split('?')[0].split('#')[0];
  }
}

/**
 * DPoP (Demonstrating Proof-of-Possession) — RFC 9449
 *
 * This module builds DPoP proof JWTs for API requests. The keypair itself is
 * managed by oidc-client-ts (`IndexedDbDPoPStore`) so the same key is used
 * for the token endpoint (binding via cnf.jkt) AND for resource server calls.
 *
 * Algorithm: ES256 (ECDSA P-256 / SHA-256)
 *   We deliberately match oidc-client-ts v3's hard-coded choice. Mixing
 *   curves between the library (P-256) and our interceptor (P-384) would
 *   either need separate keys (breaking cnf.jkt binding) or fail signature
 *   verification at the resource server.
 *
 * Flow:
 *   1. Library generates P-256 ECDSA non-extractable CryptoKeyPair.
 *      Private key never leaves the browser (extractable=false).
 *   2. Persisted in IndexedDB via the library's `IndexedDbDPoPStore`
 *      (database "oidc", store "dpop").
 *   3. Before every API request, build a signed DPoP proof JWT:
 *        { typ:"dpop+jwt", alg:"ES256", jwk:<publicKeyJwk> }
 *        { jti:<uuid>, htm:<METHOD>, htu:<url-no-query>, iat:<now>,
 *          ath:<base64url-sha256(access_token)>, nonce?:<server_nonce> }
 *   4. Attach to request:
 *        Authorization: DPoP <access_token>
 *        DPoP: <proof_jwt>
 */

const ALG = 'ES256';

// ─────────────────────────────────────────────────────────────────────────────
// JWK thumbprint (RFC 7638)
// Used to compute cnf.jkt — the binding claim that ties a token to a key.
// ─────────────────────────────────────────────────────────────────────────────

export async function computeJwkThumbprint(publicKey: CryptoKey): Promise<string> {
  const jwk = await crypto.subtle.exportKey('jwk', publicKey);
  // RFC 7638 § 3.2: sorted, minimal set of required members for EC keys.
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
  /** Full request URL (query string + fragment stripped per RFC 9449 § 4.2) */
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

  // Strip query string and fragment per RFC 9449 § 4.2.
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
    { name: 'ECDSA', hash: { name: 'SHA-256' } },
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
    // Not a full URL (relative path) — strip manually.
    return url.split('?')[0].split('#')[0];
  }
}

import { randomUUID } from 'expo-crypto';

/**
 * Idempotency keys for one kind of write (creating a booking, paying it).
 * Retrying the same request body reuses its key, so a retry after a lost response (timeout, network drop)
 * returns the original result instead of booking or charging twice. A changed body gets a fresh key, because the
 * API rejects a key reused for a different request. Call `reset` once a request reached a definite outcome.
 */
export function createIdempotencyKeys(generate: () => string = randomUUID) {
  let last: { body: string; key: string } | null = null;
  return {
    keyFor(body: unknown): string {
      const serialized = JSON.stringify(body);
      if (last?.body !== serialized) {
        last = { body: serialized, key: generate() };
      }
      return last.key;
    },
    reset() {
      last = null;
    },
  };
}

export type IdempotencyKeys = ReturnType<typeof createIdempotencyKeys>;

import { createIdempotencyKeys } from '../idempotency';

function sequence() {
  let n = 0;
  return () => `key-${++n}`;
}

describe('createIdempotencyKeys', () => {
  it('reuses the key while the request body is unchanged', () => {
    const keys = createIdempotencyKeys(sequence());

    expect(keys.keyFor({ a: 1 })).toBe('key-1');
    expect(keys.keyFor({ a: 1 })).toBe('key-1');
  });

  it('issues a new key for a different body', () => {
    const keys = createIdempotencyKeys(sequence());

    keys.keyFor({ a: 1 });

    expect(keys.keyFor({ a: 2 })).toBe('key-2');
  });

  it('issues a new key for the same body after a reset', () => {
    const keys = createIdempotencyKeys(sequence());
    keys.keyFor({ a: 1 });

    keys.reset();

    expect(keys.keyFor({ a: 1 })).toBe('key-2');
  });
});
